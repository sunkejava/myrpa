using System.Text.Json;
using AgentRPA.Application.Workflow;

namespace AgentRPA.Tests;

public sealed class WorkflowDefinitionValidatorTests
{
    private readonly WorkflowDefinitionValidator _validator = new();

    [Fact] public void Empty_definition_is_rejected() => Assert.NotEmpty(_validator.Validate(string.Empty));
    [Fact] public void Definition_without_steps_is_rejected() => Assert.Contains(_validator.Validate("{\"name\":\"demo\"}"), x => x.Contains("steps", StringComparison.OrdinalIgnoreCase));
    [Fact] public void Unknown_step_type_is_rejected() => Assert.NotEmpty(_validator.Validate("{\"steps\":[{\"type\":\"Unknown\",\"config\":{}}]}"));
    [Fact] public void Script_step_is_not_publishable() => Assert.Contains(_validator.Validate("{\"steps\":[{\"type\":\"Script\",\"config\":{\"language\":\"javascript\"}}]}"), x => x.Contains("Script", StringComparison.OrdinalIgnoreCase));
    [Fact] public void Invalid_parameter_schema_is_rejected()
    {
        var errors = _validator.Validate("{\"parameters\":{\"employeeId\":{\"type\":\"guid\",\"required\":\"yes\"}},\"steps\":[{\"type\":\"End\"}]}");
        Assert.Contains(errors, x => x.Contains("type", StringComparison.OrdinalIgnoreCase)); Assert.Contains(errors, x => x.Contains("required", StringComparison.OrdinalIgnoreCase));
    }
    [Fact] public void Valid_parameter_schema_is_accepted() => Assert.Empty(_validator.Validate("{\"parameters\":{\"employeeId\":{\"type\":\"string\",\"required\":true,\"sensitive\":false}},\"steps\":[{\"type\":\"End\"}]}"));
    [Fact] public void Parameter_values_are_checked_against_schema()
    {
        var schema = JsonDocument.Parse("{\"parameters\":{\"employeeId\":{\"type\":\"string\",\"required\":true},\"count\":{\"type\":\"integer\",\"required\":true}}}").RootElement;
        var errors = new WorkflowParameterSchemaValidator().ValidateParameters(schema, new Dictionary<string, object?> { ["employeeId"] = "A001", ["count"] = "not-an-integer" });
        Assert.Contains(errors, x => x.Contains("count", StringComparison.OrdinalIgnoreCase));
    }

    [Fact] public void String_length_and_number_range_are_checked()
    {
        var schema = JsonDocument.Parse("{\"parameters\":{\"name\":{\"type\":\"string\",\"minLength\":3,\"maxLength\":5},\"age\":{\"type\":\"integer\",\"minimum\":18,\"maximum\":60}}}").RootElement;
        var errors = new WorkflowParameterSchemaValidator().ValidateParameters(schema, new Dictionary<string, object?> { ["name"] = "ab", ["age"] = 61 });
        Assert.Equal(2, errors.Count);
    }

    [Fact] public void Enum_and_default_are_checked()
    {
        var validator = new WorkflowParameterSchemaValidator();
        var valid = JsonDocument.Parse("{\"parameters\":{\"status\":{\"type\":\"string\",\"enum\":[\"A\",\"B\"],\"default\":\"A\"}}}").RootElement;
        Assert.Empty(validator.ValidateDefinition(valid));
        var errors = validator.ValidateParameters(valid, new Dictionary<string, object?> { ["status"] = "C" });
        Assert.Contains(errors, x => x.Contains("枚举", StringComparison.Ordinal));
    }
}
