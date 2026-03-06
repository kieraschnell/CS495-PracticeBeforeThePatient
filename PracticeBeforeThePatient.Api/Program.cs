using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Core.Models;
using PracticeBeforeThePatient.Data;
using PracticeBeforeThePatient.Data.Entities;
using PracticeBeforeThePatient.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorApp", policy =>
    {
        policy.WithOrigins("https://localhost:7124", "http://localhost:5009")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "Data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "app.db");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSingleton<ClassRosterStore>();
builder.Services.AddSingleton<DevAccessStore>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Scenarios.Any())
    {
        var scenariosDir = Path.Combine(app.Environment.ContentRootPath, "Data", "scenarios");
        if (Directory.Exists(scenariosDir))
        {
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            foreach (var file in Directory.GetFiles(scenariosDir, "*.json"))
            {
                var id = Path.GetFileNameWithoutExtension(file) ?? "";
                var createdBy = "admin@ua.edu";
                var json = File.ReadAllText(file);
                var parsed = JsonSerializer.Deserialize<Scenario>(json, jsonOptions);
                var rootJson = JsonSerializer.Serialize(parsed?.Root ?? new Node(), jsonOptions);

                db.Scenarios.Add(new ScenarioEntity
                {
                    Id = id,
                    CreatedBy = string.IsNullOrWhiteSpace(parsed?.CreatedBy) ? createdBy : parsed.CreatedBy,
                    Title = parsed?.Title ?? id,
                    Description = parsed?.Description ?? "",
                    NodesJson = rootJson,
                    CreatedAt = parsed?.CreatedAt ?? DateTime.UtcNow
                });
            }
            db.SaveChanges();
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("BlazorApp");
app.UseAuthorization();
app.MapControllers();

app.Run();
