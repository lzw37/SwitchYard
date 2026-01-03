using SwitchYard.Service;

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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.UseAuthorization();

app.MapControllers();

DBConnector.SetConfiguration(builder.Configuration);

app.Run();
