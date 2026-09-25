using AgentRPA.Application.Workflow;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Security;

/// <summary>任务队列入口及派发入口共用的审批状态读取；缺失的高风险审批记录按待审批处理。</summary>
public static class TaskApprovalGate
{
    public static async Task<TaskApprovalStatus?> GetStatusAsync(AgentRpaDbContext db, Guid taskId, string definitionJson, CancellationToken ct)
    {
        var status = await db.TaskApprovals.AsNoTracking().Where(x => x.TaskId == taskId)
            .Select(x => (TaskApprovalStatus?)x.Status).SingleOrDefaultAsync(ct);
        return status ?? (WorkflowApprovalPolicy.RequiresApproval(definitionJson) ? TaskApprovalStatus.Pending : null);
    }

    public static void AddPending(AgentRpaDbContext db, RpaTask task, Guid requesterId, string definitionJson, bool requestedByAgent = false)
    {
        if (requestedByAgent || WorkflowApprovalPolicy.RequiresApproval(definitionJson))
            db.TaskApprovals.Add(new TaskApproval(task.Id, requesterId));
    }
}
