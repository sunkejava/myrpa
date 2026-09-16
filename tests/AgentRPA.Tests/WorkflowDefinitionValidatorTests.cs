using AgentRPA.Application.Workflow;

namespace AgentRPA.Tests;

public sealed class WorkflowDefinitionValidatorTests
{
    private readonly WorkflowDefinitionValidator _validator = new();

    [Fact]
    public void Empty_definition_is_rejected()
    {
        var errors = _validator.Validate(string.Empty);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Definition_without_steps_is_rejected()
    {
        var errors = _validator.Validate("{\"name\":\"demo\"}");

        Assert.Contains(errors, x => x.Contains("steps", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Unknown_step_type_is_rejected()
    {
        var errors = _validator.Validate("{\"steps\":[{\"type\":\"Unknown\",\"config\":{}}]}");

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Script_step_is_not_publishable()
    {
        var errors = _validator.Validate("{\"steps\":[{\"type\":\"Script\",\"config\":{\"language\":\"javascript\"}}]}");

        Assert.Contains(errors, x => x.Contains("Script", StringComparison.OrdinalIgnoreCase));
    }
}
