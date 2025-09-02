using SqliteWebDemoApi.Options;
using SqliteWebDemoApi.Repositories;
using SqliteWebDemoApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection("ConnectionStrings"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Default),
        "ConnectionStrings:Default must not be empty.")
    .ValidateOnStart();

builder.Services.AddScoped<ISqliteRepository, SqliteRepository>();
builder.Services.AddScoped<ISqliteService, SqliteService>();
builder.Services.AddControllers();

builder.Services.AddCors(o =>
    o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors("AllowAll");
app.MapControllers();
app.Run();