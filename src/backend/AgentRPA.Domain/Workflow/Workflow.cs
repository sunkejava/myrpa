using AgentRPA.Domain.Common;
namespace AgentRPA.Domain.Workflow;
public enum WorkflowStatus { Draft = 0, Published = 1, Disabled = 2 }
public enum WorkflowStepType { Navigate, Click, Input, Select, Wait, WaitForElement, Extract, Upload, Download, Screenshot, Script, Condition, Loop, SubWorkflow, HumanTask, Assert, End, UKeySign }
public sealed class Workflow : Entity
{
    private readonly List<WorkflowVersion> _versions = [];
    private Workflow() { }
    public Workflow(Guid businessFunctionId, string name, string? description = null) { BusinessFunctionId = businessFunctionId; Name = name; Description = description; }
    public Guid BusinessFunctionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public WorkflowStatus Status { get; private set; } = WorkflowStatus.Draft;
    public IReadOnlyCollection<WorkflowVersion> Versions => _versions;
    public WorkflowVersion AddVersion(string definitionJson) { var version = new WorkflowVersion(Id, _versions.Count + 1, definitionJson); _versions.Add(version); return version; }
    public void Publish() => Status = WorkflowStatus.Published;
    public void Disable() => Status = WorkflowStatus.Disabled;
}
public sealed class WorkflowVersion : Entity
{
    private readonly List<WorkflowStep> _steps = [];
    private WorkflowVersion() { }
    public WorkflowVersion(Guid workflowId, int version, string definitionJson) { WorkflowId = workflowId; Version = version; DefinitionJson = definitionJson; }
    public Guid WorkflowId { get; private set; }
    public int Version { get; private set; }
    public string DefinitionJson { get; private set; } = "{}";
    public bool Published { get; private set; }
    public IReadOnlyCollection<WorkflowStep> Steps => _steps;
    public void AddStep(WorkflowStep step) => _steps.Add(step);
    public void Publish() => Published = true;
}
public sealed class WorkflowStep : Entity
{
    private WorkflowStep() { }
    public WorkflowStep(Guid workflowVersionId, int order, WorkflowStepType type, string name, string configJson) { WorkflowVersionId = workflowVersionId; Order = order; Type = type; Name = name; ConfigJson = configJson; }
    public Guid WorkflowVersionId { get; private set; }
    public int Order { get; private set; }
    public WorkflowStepType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ConfigJson { get; private set; } = "{}";
}
