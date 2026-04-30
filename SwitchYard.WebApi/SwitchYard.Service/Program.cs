using SwitchYard.Service;
using SwitchYard.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using SwitchYard.Service.Utils;
using Serilog;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

// 配置 Serilog - 在创建 builder 之前
var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logDirectory);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logDirectory, "switchyard-.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 10485760,
        rollOnFileSizeLimit: true,
        shared: true,
        flushToDiskInterval: TimeSpan.FromSeconds(1))
    .CreateLogger();

try
{
    Log.Information("Starting SwitchYard Service");

    var builder = WebApplication.CreateBuilder(args);

    // 使用 Serilog 作为日志提供程序
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // 允许序列化 NaN/Infinity 等非有限数（作为兜底，避免计算异常导致 500）
            options.JsonSerializerOptions.NumberHandling =
                System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
        });

    // 配置代理头部转发 - 用于获取真实客户端IP
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        // 清除默认的已知网络和代理
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        
        // 配置要处理的头部
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        
        // 如果您知道代理服务器的IP，可以添加到已知代理列表中
        // 通常包括本地回环地址和私有网络地址
        options.KnownProxies.Add(IPAddress.Loopback); // 127.0.0.1
        options.KnownProxies.Add(IPAddress.IPv6Loopback); // ::1
        
        // 如果在容器或云环境中，可能需要添加特定的网络
        // 例如 Docker 的默认网络
        // options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("172.17.0.0"), 16));
        
        // 在生产环境中，为了安全考虑，应该限制已知代理
        // 在开发环境中，可以允许所有代理
        if (builder.Environment.IsDevelopment())
        {
            // 开发环境：允许任何代理
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();
        }
        else
        {
            // 生产环境：添加您的Nginx代理服务器IP
            // 请根据您的实际部署环境修改以下IP地址
            // options.KnownProxies.Add(IPAddress.Parse("nginx_server_ip"));
            
            // 或者添加整个内网网段
            options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
            options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
            options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
        }
        
        // 设置转发限制（防止头部欺骗）
        options.ForwardLimit = 2; // 允许最多经过2层代理
    });

    // Configure CORS: 从配置文件读取允许的域名
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

    builder.Services.AddCors(options =>
    {
        // 开发环境策略 - 允许所有源
        options.AddPolicy("Development", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
        
        // 生产环境策略 - 只允许配置的域名
        options.AddPolicy("Production", policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            }
            else
            {
                // 如果没有配置域名，使用默认的安全设置
                policy.WithOrigins("https://yourdomain.com")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            }
        });
        
        // 动态策略 - 根据环境自动选择
        options.AddPolicy("Dynamic", policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
            else
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                }
            }
        });
    });

    // 注册JWT服务
    builder.Services.AddSingleton<JwtTokenService>();
    builder.Services.AddSingleton<RefreshTokenService>();
    builder.Services.AddSingleton<DatabaseSchemaInitializer>();
    builder.Services.AddScoped<UserService>();
    builder.Services.AddScoped<InstanceAuthorizationService>();
    builder.Services.AddScoped<HumpInstanceCopyService>();
    builder.Services.AddSingleton<SnowflakeIdGenerator>();

    // 配置JWT认证
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var secretKey = jwtSettings["SecretKey"] ?? throw new ArgumentNullException("Jwt:SecretKey");
    var issuer = jwtSettings["Issuer"] ?? "SwitchYard.Service";
    var audience = jwtSettings["Audience"] ?? "SwitchYard.Client";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

    builder.Services.AddAuthorization();

    // 速率限制：针对认证端点按客户端 IP 限流，防止暴力破解与账号枚举。
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy("auth", httpContext =>
        {
            var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
        });

        options.AddPolicy("register", httpContext =>
        {
            var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
        });
    });

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        // 添加JWT认证支持到Swagger
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT授权(数据将在请求头中进行传输) 直接在下框中输入Bearer {token}（注意两者之间是一个空格）",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            BearerFormat = "JWT",
            Scheme = "Bearer"
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    // 生产环境敏感配置校验：必须由环境变量注入，appsettings 不应保留明文。
    if (app.Environment.IsProduction())
    {
        var missingSecrets = new List<string>();

        if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:SecretKey"]))
        {
            missingSecrets.Add("Jwt__SecretKey");
        }

        var dbType = builder.Configuration["HumpDatabase:DatabaseType"] ?? "Sqllite";
        if (dbType.Equals("Mysql", StringComparison.OrdinalIgnoreCase) ||
            dbType.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(builder.Configuration["HumpDatabase:MysqlConfig:Username"]))
            {
                missingSecrets.Add("HumpDatabase__MysqlConfig__Username");
            }
            if (string.IsNullOrWhiteSpace(builder.Configuration["HumpDatabase:MysqlConfig:Password"]))
            {
                missingSecrets.Add("HumpDatabase__MysqlConfig__Password");
            }
        }

        if (missingSecrets.Count > 0)
        {
            throw new InvalidOperationException(
                "Production startup aborted: missing required secret environment variable(s): " +
                string.Join(", ", missingSecrets) +
                ". See scripts/deploy/switchyard-api.env.example.");
        }
    }

    // 添加启动日志
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("=== SwitchYard Service Starting ===");
    logger.LogInformation("Application starting in {Environment} environment", app.Environment.EnvironmentName);
    logger.LogInformation("Content Root: {ContentRoot}", app.Environment.ContentRootPath);
    logger.LogInformation("Web Root: {WebRoot}", app.Environment.WebRootPath);
    logger.LogInformation("Log Directory: {LogDirectory}", logDirectory);
    logger.LogInformation("Allowed CORS Origins: {Origins}", string.Join(", ", allowedOrigins));
    logger.LogInformation("JWT Issuer: {Issuer}, Audience: {Audience}", issuer, audience);

    // Configure the HTTP request pipeline.
    
    // 必须在所有其他中间件之前添加ForwardedHeaders
    app.UseForwardedHeaders();
    logger.LogInformation("ForwardedHeaders middleware enabled for proxy support");

    // 安全响应头（对所有响应生效）
    app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        if (!context.Request.IsHttps)
        {
            // HSTS 仅对 HTTPS 响应有意义，不设置。
        }
        await next();
    });

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
    
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        logger.LogInformation("Swagger enabled for development environment");
    }

    app.UseHttpsRedirection();

    // 重要：CORS必须在认证和授权之前启用
    // 使用动态策略，自动根据环境选择合适的CORS设置
    app.UseCors("Dynamic");
    logger.LogInformation("CORS enabled with Dynamic policy");

    // 启用认证和授权中间件
    app.UseAuthentication();
    app.UseAuthorization();
    logger.LogInformation("Authentication and Authorization enabled");

    // 启用速率限制（需位于路由之后、端点映射之前）
    app.UseRateLimiter();
    logger.LogInformation("RateLimiter enabled");

    app.MapControllers();

    DBConnector.SetConfiguration(builder.Configuration);
    logger.LogInformation("Database connector configured");

    // 确保 refreshtoken 表已创建
    var databaseSchemaInitializer = app.Services.GetRequiredService<DatabaseSchemaInitializer>();
    databaseSchemaInitializer.EnsureSchemaCreated();
    logger.LogInformation("Database schema initialized");

    logger.LogInformation("Application configured successfully, starting to listen for requests...");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
