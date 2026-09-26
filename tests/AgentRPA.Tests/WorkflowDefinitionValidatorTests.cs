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
    [Fact] public void Extract_cannot_overwrite_system_base_url()
    {
        const string definition = """{"steps":[{"type":"Extract","config":{"selector":"#url","output":"SystemBaseUrl"}}]}""";
        Assert.Contains(_validator.Validate(definition), x => x.Contains("systemBaseUrl", StringComparison.OrdinalIgnoreCase));
    }
    [Fact] public void Nested_extract_cannot_overwrite_declared_task_parameter()
    {
        const string definition = """{"parameters":{"personId":{"type":"string"}},"steps":[{"type":"Loop","config":{"steps":[{"type":"Extract","config":{"selector":"#id","output":"PersonId"}}]}}]}""";
        Assert.Contains(_validator.Validate(definition), x => x.Contains("不能覆盖任务参数", StringComparison.Ordinal));
    }
    [Fact] public void Unsupported_nested_action_cannot_be_published()
    {
        const string definition = """{"steps":[{"type":"Condition","config":{"then":[{"type":"Click","requiredAction":"SuperAdmin","config":{"selector":"#submit"}}]}}]}""";
        Assert.Contains(_validator.Validate(definition), x => x.Contains("requiredAction", StringComparison.Ordinal));
    }
    [Fact] public void Mutating_step_cannot_be_retried()
    {
        const string definition = """{"steps":[{"type":"Click","retryCount":1,"config":{"selector":"#submit"}}]}""";
        Assert.Contains(_validator.Validate(definition), x => x.Contains("retryCount", StringComparison.Ordinal));
    }

    [Fact] public void Nested_step_timeout_and_retry_limits_are_validated()
    {
        const string invalid = """{"steps":[{"type":"Loop","config":{"steps":[{"type":"Assert","timeoutMs":50,"retryCount":4,"config":{"selector":"#ready"}}]}}]}""";
        var errors = _validator.Validate(invalid);
        Assert.Contains(errors, x => x.Contains("timeoutMs", StringComparison.Ordinal));
        Assert.Contains(errors, x => x.Contains("retryCount", StringComparison.Ordinal));
        const string valid = """{"steps":[{"type":"WaitForElement","timeoutMs":1000,"retryCount":2,"config":{"selector":"#ready"}}]}""";
        Assert.Empty(_validator.Validate(valid));
    }
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
