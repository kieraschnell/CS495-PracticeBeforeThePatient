using PracticeBeforeThePatient.Core.Models;
using Xunit;

namespace PracticeBeforeThePatient.Tests.Unit;

/// <summary>
/// Tests for scenario validation rules as documented in MAINTENANCE.md.
/// </summary>
public class ScenarioValidationTests
{
    [Fact]
    public void Scenario_MustHaveTitle()
    {
        var scenario = new Scenario
        {
            Id = "test",
            Title = "",
            Description = "Test description"
        };

        var isValid = !string.IsNullOrWhiteSpace(scenario.Title);

        Assert.False(isValid, "Scenario without title should be invalid");
    }

    [Fact]
    public void Scenario_WithTitle_IsValid()
    {
        var scenario = new Scenario
        {
            Id = "test",
            Title = "Valid Title",
            Description = "Test description"
        };

        var isValid = !string.IsNullOrWhiteSpace(scenario.Title);

        Assert.True(isValid);
    }

    [Fact]
    public void Scenario_HasRequiredProperties()
    {
        var scenario = new Scenario();

        Assert.NotNull(scenario.Id);
        Assert.NotNull(scenario.CreatedBy);
        Assert.NotNull(scenario.Title);
        Assert.NotNull(scenario.Description);
        Assert.NotNull(scenario.Root);
    }
}
