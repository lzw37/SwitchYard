using SwitchYard.Service;
using SwitchYard.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Bind to URLs configured in appsettings.json (WebApi:Hosts)
//var hosts = builder.Configuration.GetSection("WebApi:Hosts").Get<string[]>();
//if (hosts != null && hosts.Length >0)
//{
// builder.WebHost.UseUrls(hosts);
//}

// Add services to the container.

builder.Services.AddControllers();

// Configure CORS: allow all origins, methods and headers (update as needed for production)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
           .AllowAnyHeader();
    });
});

// 注册JWT服务
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<UserService>();

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowAll");

// 启用认证和授权中间件
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

DBConnector.SetConfiguration(builder.Configuration);

app.Run();
