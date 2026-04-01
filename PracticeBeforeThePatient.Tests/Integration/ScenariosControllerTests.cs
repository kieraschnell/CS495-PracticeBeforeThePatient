using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PracticeBeforeThePatient.Api.Controllers;
using PracticeBeforeThePatient.Data;
using PracticeBeforeThePatient.Tests.Fixtures;
using Xunit;

namespace PracticeBeforeThePatient.Tests.Integration;

public class ScenariosControllerTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;
    private readonly ApiTestFixture _fixture;

    public ScenariosControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    private async Task SetupTeacherUserAsync()
    {
        // Create a teacher user and set them as the current dev user
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Users.Any(u => u.Email == "scenario-teacher@ua.edu"))
            {
                TestDataSeeder.CreateTeacher(db, "scenario-teacher@ua.edu");
            }
        }

        await _client.PostAsJsonAsync("/api/access/dev-user",
            new AccessController.SetDevUserRequest { Email = "scenario-teacher@ua.edu" });
    }

    [Fact]
    public async Task GetScenarios_ReturnsOk()
    {
        await SetupTeacherUserAsync();
        
        var response = await _client.GetAsync("/api/scenarios");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetScenarios_ReturnsListOfScenarioIds()
    {
        // Arrange - create teacher user and scenarios
        await SetupTeacherUserAsync();
        
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Scenarios.Any(s => s.Id == "list-scenario-1"))
            {
                TestDataSeeder.CreateScenario(db, "list-scenario-1", "List Scenario 1");
            }
            if (!db.Scenarios.Any(s => s.Id == "list-scenario-2"))
            {
                TestDataSeeder.CreateScenario(db, "list-scenario-2", "List Scenario 2");
            }
        }

        // Act
        var scenarios = await _client.GetFromJsonAsync<List<string>>("/api/scenarios");

        // Assert
        Assert.NotNull(scenarios);
        Assert.Contains("list-scenario-1", scenarios);
        Assert.Contains("list-scenario-2", scenarios);
    }

    [Fact]
    public async Task GetScenarioById_ReturnsScenario_WhenExists()
    {
        // Arrange
        await SetupTeacherUserAsync();
        
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Scenarios.Any(s => s.Id == "get-by-id-scenario"))
            {
                TestDataSeeder.CreateScenario(db, "get-by-id-scenario", "Get By ID Scenario");
            }
        }

        // Act
        var response = await _client.GetAsync("/api/scenarios/get-by-id-scenario");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetScenarioById_ReturnsNotFound_WhenNotExists()
    {
        // Arrange - need a teacher to avoid Forbid response
        await SetupTeacherUserAsync();
        
        var response = await _client.GetAsync("/api/scenarios/nonexistent-scenario-12345");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
