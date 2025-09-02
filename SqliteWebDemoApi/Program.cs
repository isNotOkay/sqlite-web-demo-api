using Microsoft.Data.Sqlite;
using SqliteWebDemoApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- CORS: allow any origin (dev) ----
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p => p
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

// Controllers
builder.Services.AddControllers();

// Connection string (fail early if missing)
var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");

// Register a simple connection factory and the browser service
builder.Services.AddScoped<Func<SqliteConnection>>(_ => () => new SqliteConnection(connectionString));
builder.Services.AddScoped<ISqliteBrowser, SqliteBrowser>();

var app = builder.Build();

app.UseCors("AllowAll");

app.MapControllers();

app.Run();