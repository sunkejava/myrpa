namespace AgentRPA.Contracts.Nodes;

/// <summary>Server → NodeAgent 的实时命令接口。</summary>
public interface INodeAgentClient
{
    Task ExecuteAsync(ExecutionCommand command);
    Task CancelAsync(Guid executionId);
    Task PauseAsync(Guid executionId);
    Task ResumeAsync(Guid executionId);
}

/// <summary>NodeAgent → Server 的连接初始化信息。</summary>
public sealed record NodeAgentConnectRequest(Guid NodeId, string AgentKey, string AgentVersion);
