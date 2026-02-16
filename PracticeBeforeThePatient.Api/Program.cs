using System.Text.Json.Serialization;
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
        policy.WithOrigins("https://localhost:7168", "http://localhost:5168")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<ClassRosterStore>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("BlazorApp");

app.MapControllers();

app.Run();
