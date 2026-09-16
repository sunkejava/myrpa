using System.Text.Json;
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

    [Fact]
    public void Invalid_parameter_schema_is_rejected()
    {
        var errors = _validator.Validate("{\"parameters\":{\"employeeId\":{\"type\":\"guid\",\"required\":\"yes\"}},\"steps\":[{\"type\":\"End\"}]}");
        Assert.Contains(errors, x => x.Contains("type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, x => x.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Valid_parameter_schema_is_accepted()
    {
        var errors = _validator.Validate("{\"parameters\":{\"employeeId\":{\"type\":\"string\",\"required\":true,\"sensitive\":false}},\"steps\":[{\"type\":\"End\"}]}");
        Assert.Empty(errors);
    }

    [Fact]
    public void Parameter_values_are_checked_against_schema()
    {
        var schema = JsonDocument.Parse("{\"parameters\":{\"employeeId\":{\"type\":\"string\",\"required\":true},\"count\":{\"type\":\"integer\",\"required\":true}}}").RootElement;
        var validator = new WorkflowParameterSchemaValidator();
        var errors = validator.ValidateParameters(schema, new Dictionary<string, object?> { ["employeeId"] = "A001", ["count"] = "not-an-integer" });
        Assert.Contains(errors, x => x.Contains("count", StringComparison.OrdinalIgnoreCase));
    }
}
