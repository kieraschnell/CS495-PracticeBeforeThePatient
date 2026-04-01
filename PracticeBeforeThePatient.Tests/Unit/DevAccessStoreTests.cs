using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PracticeBeforeThePatient.Data;
using PracticeBeforeThePatient.Services;
using PracticeBeforeThePatient.Tests.Fixtures;
using Xunit;

namespace PracticeBeforeThePatient.Tests.Unit;

public class DevAccessStoreTests
{
    private static (IServiceScopeFactory scopeFactory, IServiceScope scope, AppDbContext db) CreateServices()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => 
            options.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        
        // Create a scope but don't dispose it - caller is responsible for disposing
        var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        return (scopeFactory, scope, db);
    }

    [Fact]
    public async Task GetCurrentEmailAsync_ReturnsDefaultEmail_WhenNotSet()
    {
        var (scopeFactory, scope, _) = CreateServices();
        using var _ = scope;
        var store = new DevAccessStore(scopeFactory);

        var email = await store.GetCurrentEmailAsync();

        Assert.Equal("admin@ua.edu", email);
    }

    [Fact]
    public async Task SetCurrentEmailAsync_UpdatesCurrentEmail()
    {
        var (scopeFactory, scope, _) = CreateServices();
        using var _ = scope;
        var store = new DevAccessStore(scopeFactory);

        await store.SetCurrentEmailAsync("student@ua.edu");
        var email = await store.GetCurrentEmailAsync();

        Assert.Equal("student@ua.edu", email);
    }

    [Fact]
    public async Task GetCurrentRoleAsync_ReturnsAdminRole_ForAdminUser()
    {
        var (scopeFactory, scope, db) = CreateServices();
        using var _ = scope;
        TestDataSeeder.CreateAdmin(db);
        
        var store = new DevAccessStore(scopeFactory);
        await store.SetCurrentEmailAsync("admin@ua.edu");

        var role = await store.GetCurrentRoleAsync();

        Assert.Equal(DevAccessStore.AdminRole, role);
    }

    [Fact]
    public async Task GetCurrentRoleAsync_ReturnsTeacherRole_ForTeacherUser()
    {
        var (scopeFactory, scope, db) = CreateServices();
        using var _ = scope;
        TestDataSeeder.CreateTeacher(db, "instructor@ua.edu");
        
        var store = new DevAccessStore(scopeFactory);
        await store.SetCurrentEmailAsync("instructor@ua.edu");

        var role = await store.GetCurrentRoleAsync();

        Assert.Equal(DevAccessStore.TeacherRole, role);
    }

    [Fact]
    public async Task GetCurrentRoleAsync_ReturnsStudentRole_ForStudentUser()
    {
        var (scopeFactory, scope, db) = CreateServices();
        using var _ = scope;
        TestDataSeeder.CreateStudent(db, "student1@ua.edu");
        
        var store = new DevAccessStore(scopeFactory);
        await store.SetCurrentEmailAsync("student1@ua.edu");

        var role = await store.GetCurrentRoleAsync();

        Assert.Equal(DevAccessStore.StudentRole, role);
    }

    [Fact]
    public async Task IsTeacherAsync_ReturnsTrue_ForTeacher()
    {
        var (scopeFactory, scope, db) = CreateServices();
        using var _ = scope;
        TestDataSeeder.CreateTeacher(db, "instructor@ua.edu");
        
        var store = new DevAccessStore(scopeFactory);
        await store.SetCurrentEmailAsync("instructor@ua.edu");

        var isTeacher = await store.IsTeacherAsync();

        Assert.True(isTeacher);
    }

    [Fact]
    public async Task IsTeacherAsync_ReturnsTrue_ForAdmin()
    {
        var (scopeFactory, scope, db) = CreateServices();
        using var _ = scope;
        TestDataSeeder.CreateAdmin(db);
        
        var store = new DevAccessStore(scopeFactory);
        await store.SetCurrentEmailAsync("admin@ua.edu");

        var isTeacher = await store.IsTeacherAsync();

        Assert.True(isTeacher);
    }

    [Fact]
    public async Task IsTeacherAsync_ReturnsFalse_ForStudent()
    {
        var (scopeFactory, scope, db) = CreateServices();
        using var _ = scope;
        TestDataSeeder.CreateStudent(db, "student@ua.edu");
        
        var store = new DevAccessStore(scopeFactory);
        await store.SetCurrentEmailAsync("student@ua.edu");

        var isTeacher = await store.IsTeacherAsync();

        Assert.False(isTeacher);
    }

    [Fact]
    public async Task IsAdminAsync_ReturnsTrue_OnlyForAdmin()
    {
        var (scopeFactory, scope, db) = CreateServices();
        using var _ = scope;
        TestDataSeeder.CreateAdmin(db);
        TestDataSeeder.CreateTeacher(db, "teacher@ua.edu");
        
        var store = new DevAccessStore(scopeFactory);

        await store.SetCurrentEmailAsync("admin@ua.edu");
        Assert.True(await store.IsAdminAsync());

        await store.SetCurrentEmailAsync("teacher@ua.edu");
        Assert.False(await store.IsAdminAsync());
    }

    [Theory]
    [InlineData("admin", "admin")]
    [InlineData("ADMIN", "admin")]
    [InlineData("teacher", "teacher")]
    [InlineData("instructor", "teacher")]
    [InlineData("student", "student")]
    [InlineData("unknown", "student")]
    [InlineData(null, "student")]
    public void NormalizeRole_ReturnsExpectedRole(string? input, string expected)
    {
        var result = DevAccessStore.NormalizeRole(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task SetThemeForCurrentEmailAsync_SetsTheme()
    {
        var (scopeFactory, scope, db) = CreateServices();
        using var _ = scope;
        TestDataSeeder.CreateAdmin(db);
        
        var store = new DevAccessStore(scopeFactory);
        await store.SetCurrentEmailAsync("admin@ua.edu");

        var result = await store.SetThemeForCurrentEmailAsync("dark");
        var theme = await store.GetThemeForCurrentEmailAsync();

        Assert.True(result);
        Assert.Equal(DevAccessStore.DarkTheme, theme);
    }

    [Fact]
    public async Task SetThemeForCurrentEmailAsync_ReturnsFalse_WhenNoUserSet()
    {
        var (scopeFactory, scope, _) = CreateServices();
        using var _ = scope;
        var store = new DevAccessStore(scopeFactory);
        await store.SetCurrentEmailAsync("");

        var result = await store.SetThemeForCurrentEmailAsync("dark");

        Assert.False(result);
    }

    [Fact]
    public async Task GetThemeForCurrentEmailAsync_ReturnsLightByDefault()
    {
        var (scopeFactory, scope, db) = CreateServices();
        using var _ = scope;
        TestDataSeeder.CreateAdmin(db);
        
        var store = new DevAccessStore(scopeFactory);
        await store.SetCurrentEmailAsync("admin@ua.edu");

        var theme = await store.GetThemeForCurrentEmailAsync();

        Assert.Equal(DevAccessStore.LightTheme, theme);
    }
}
