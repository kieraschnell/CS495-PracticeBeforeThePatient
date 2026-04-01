using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PracticeBeforeThePatient.Api.Controllers;
using PracticeBeforeThePatient.Data;
using PracticeBeforeThePatient.Tests.Fixtures;
using Xunit;

namespace PracticeBeforeThePatient.Tests.Integration;

public class AccessControllerTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;
    private readonly ApiTestFixture _fixture;

    public AccessControllerTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetAccess_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/access");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAccess_ReturnsAccessResponse()
    {
        var response = await _client.GetFromJsonAsync<AccessController.AccessResponse>("/api/access");

        Assert.NotNull(response);
        Assert.NotNull(response.Email);
        Assert.NotNull(response.Role);
        Assert.NotNull(response.AllowedScenarioIds);
        Assert.NotNull(response.AllowedScenarioOptions);
    }

    [Fact]
    public async Task SetDevUser_ChangesCurrentUser()
    {
        // Arrange - seed a student user
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            TestDataSeeder.CreateStudent(db, "testswitch@ua.edu");
        }

        // Act
        var request = new AccessController.SetDevUserRequest { Email = "testswitch@ua.edu" };
        var response = await _client.PostAsJsonAsync("/api/access/dev-user", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AccessController.AccessResponse>();
        Assert.NotNull(result);
        Assert.Equal("testswitch@ua.edu", result.Email);
    }

    [Fact]
    public async Task SetDevUser_ReturnsBadRequest_WhenBodyIsNull()
    {
        var response = await _client.PostAsync("/api/access/dev-user", null);

        // Empty body should result in BadRequest or UnsupportedMediaType
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task SetTheme_UpdatesUserTheme()
    {
        // Arrange
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            TestDataSeeder.CreateAdmin(db, "themetest@ua.edu");
        }
        await _client.PostAsJsonAsync("/api/access/dev-user",
            new AccessController.SetDevUserRequest { Email = "themetest@ua.edu" });

        // Act
        var request = new AccessController.SetThemeRequest { Theme = "dark" };
        var response = await _client.PostAsJsonAsync("/api/access/theme", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AccessController.AccessResponse>();
        Assert.NotNull(result);
        Assert.Equal("dark", result.Theme);
    }

    [Fact]
    public async Task AdminUser_HasTeacherAndAdminFlags()
    {
        // Arrange
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            TestDataSeeder.CreateAdmin(db, "adminflags@ua.edu");
        }

        // Act
        await _client.PostAsJsonAsync("/api/access/dev-user",
            new AccessController.SetDevUserRequest { Email = "adminflags@ua.edu" });
        var response = await _client.GetFromJsonAsync<AccessController.AccessResponse>("/api/access");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsAdmin);
        Assert.True(response.IsTeacher); // Admins are also teachers
    }

    [Fact]
    public async Task TeacherUser_HasTeacherFlagOnly()
    {
        // Arrange
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            TestDataSeeder.CreateTeacher(db, "teacherflags@ua.edu");
        }

        // Act
        await _client.PostAsJsonAsync("/api/access/dev-user",
            new AccessController.SetDevUserRequest { Email = "teacherflags@ua.edu" });
        var response = await _client.GetFromJsonAsync<AccessController.AccessResponse>("/api/access");

        // Assert
        Assert.NotNull(response);
        Assert.False(response.IsAdmin);
        Assert.True(response.IsTeacher);
    }

    [Fact]
    public async Task StudentUser_HasNoElevatedFlags()
    {
        // Arrange
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            TestDataSeeder.CreateStudent(db, "studentflags@ua.edu");
        }

        // Act
        await _client.PostAsJsonAsync("/api/access/dev-user",
            new AccessController.SetDevUserRequest { Email = "studentflags@ua.edu" });
        var response = await _client.GetFromJsonAsync<AccessController.AccessResponse>("/api/access");

        // Assert
        Assert.NotNull(response);
        Assert.False(response.IsAdmin);
        Assert.False(response.IsTeacher);
    }

    [Fact]
    public async Task Student_SeesOnlyAssignedScenarios()
    {
        // Arrange
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = TestDataSeeder.CreateAdmin(db, "admin-assign@ua.edu");
            var student = TestDataSeeder.CreateStudent(db, "student-assign@ua.edu");
            var classEntity = TestDataSeeder.CreateClass(db, admin, "Assignment Test Class");
            TestDataSeeder.AddStudentToClass(db, classEntity, student, admin);
            var scenario = TestDataSeeder.CreateScenario(db, "assigned-scenario", "Assigned Scenario");
            TestDataSeeder.CreateAssignment(db, classEntity, scenario, admin, "Test Assignment");

            // Also create a scenario that's NOT assigned
            TestDataSeeder.CreateScenario(db, "unassigned-scenario", "Unassigned Scenario");
        }

        // Act
        await _client.PostAsJsonAsync("/api/access/dev-user",
            new AccessController.SetDevUserRequest { Email = "student-assign@ua.edu" });
        var response = await _client.GetFromJsonAsync<AccessController.AccessResponse>("/api/access");

        // Assert
        Assert.NotNull(response);
        Assert.Contains("assigned-scenario", response.AllowedScenarioIds);
        Assert.DoesNotContain("unassigned-scenario", response.AllowedScenarioIds);
    }

    [Fact]
    public async Task Teacher_SeesAllScenarios()
    {
        // Arrange
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            TestDataSeeder.CreateTeacher(db, "teacher-all@ua.edu");
            TestDataSeeder.CreateScenario(db, "scenario-a", "Scenario A");
            TestDataSeeder.CreateScenario(db, "scenario-b", "Scenario B");
        }

        // Act
        await _client.PostAsJsonAsync("/api/access/dev-user",
            new AccessController.SetDevUserRequest { Email = "teacher-all@ua.edu" });
        var response = await _client.GetFromJsonAsync<AccessController.AccessResponse>("/api/access");

        // Assert
        Assert.NotNull(response);
        Assert.Contains("scenario-a", response.AllowedScenarioIds);
        Assert.Contains("scenario-b", response.AllowedScenarioIds);
    }
}
