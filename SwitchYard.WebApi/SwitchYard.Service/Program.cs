using SwitchYard.Service;
using SwitchYard.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SwitchYard.Service.Utils;

var builder = WebApplication.CreateBuilder(args);

// 配置日志 - 确保控制台日志在生产环境下也可用
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

// Add services to the container.
builder.Services.AddControllers();

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
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<InstanceAuthorizationService>();
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

// 添加启动日志
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("=== SwitchYard Service Starting ===");
logger.LogInformation("Application starting in {Environment} environment", app.Environment.EnvironmentName);
logger.LogInformation("Content Root: {ContentRoot}", app.Environment.ContentRootPath);
logger.LogInformation("Web Root: {WebRoot}", app.Environment.WebRootPath);
logger.LogInformation("Allowed CORS Origins: {Origins}", string.Join(", ", allowedOrigins));
logger.LogInformation("JWT Issuer: {Issuer}, Audience: {Audience}", issuer, audience);

// Configure the HTTP request pipeline.
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

app.MapControllers();

DBConnector.SetConfiguration(builder.Configuration);
logger.LogInformation("Database connector configured");

logger.LogInformation("Application configured successfully, starting to listen for requests...");

app.Run();
