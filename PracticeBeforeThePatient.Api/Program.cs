using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Api.Data;
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

// Add Entity Framework Core with SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=practicebeforethepatient.db"));

builder.Services.AddSingleton<ClassRosterStore>();
builder.Services.AddSingleton<DevAccessStore>();

var app = builder.Build();

app.UseCors("BlazorApp");
app.UseAuthorization();
app.MapControllers();

app.Run();
