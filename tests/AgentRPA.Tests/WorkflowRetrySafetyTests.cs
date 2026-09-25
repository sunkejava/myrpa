using AgentRPA.Application.Workflow;

namespace AgentRPA.Tests;

public sealed class WorkflowRetrySafetyTests
{
    [Theory]
    [InlineData("""{"steps":[{"type":"Navigate"},{"type":"WaitForElement"},{"type":"Extract"}]}""", true)]
    [InlineData("""{"steps":[{"type":"Click","config":{"selector":"#submit"}}]}""", false)]
    [InlineData("""{"steps":[{"type":"Download"}]}""", false)]
    [InlineData("""{"riskLevel":"High","steps":[{"type":"Assert"}]}""", false)]
    [InlineData("""{"steps":[{"type":"Loop","config":{"steps":[{"type":"Input"}]}}]}""", false)]
    [InlineData("""{"steps":[{"type":"Condition","config":{"then":[{"type":"Assert"}],"else":[{"type":"Extract"}]}}]}""", true)]
    [InlineData("""{"steps":[{"type":"Unexpected"}]}""", false)]
    public void Only_known_read_only_flows_are_retried(string definition, bool expected)
        => Assert.Equal(expected, WorkflowRetrySafety.IsSafeToRetry(definition));
}
