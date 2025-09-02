using SqliteWebDemoApi.Repositories;
using SqliteWebDemoApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p => p
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");

builder.Services.AddScoped<ISqliteRepository>(_ => new SqliteRepository(connectionString));
builder.Services.AddScoped<ISqliteBrowser, SqliteBrowser>();

var app = builder.Build();

app.UseCors("AllowAll");
app.MapControllers();
app.Run();