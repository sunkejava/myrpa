using AgentRPA.Api.Hubs;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Domain.Execution;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
namespace AgentRPA.Api.Controllers;
[ApiController, Route("api/executions")]
public sealed class ExecutionDispatchController(AgentRpaDbContext db, NodeAgentConnectionRegistry connections, IHubContext<NodeAgentHub, INodeAgentClient> hub) : ControllerBase
{
 [HttpPost("dispatch")]
 public async Task<IActionResult> Dispatch(DispatchRequest r, CancellationToken ct) {
  var item=await db.TaskItems.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==r.TaskItemId,ct); if(item is null)return NotFound();
  var task=await db.Tasks.AsNoTracking().SingleAsync(x=>x.Id==item.TaskId,ct); var v=await db.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(x=>x.WorkflowId==task.WorkflowId&&x.Version==task.WorkflowVersion,ct);
  var node=await db.ExecutionNodes.SingleOrDefaultAsync(x=>x.Id==r.NodeId&&x.Status==NodeStatus.Online,ct); var slot=node is null?null:await db.WorkerSlots.SingleOrDefaultAsync(x=>x.Id==r.WorkerSlotId&&x.NodeId==node.Id,ct);
  if(v is null||node is null||slot is null||!slot.IsAvailable(DateTimeOffset.UtcNow))return BadRequest(new{message="执行资源不可用。"});
  var e=new Execution(item.Id,v.Id); e.Dispatch(node.Id,slot.Id); slot.Acquire(e.Id,DateTimeOffset.UtcNow.AddMinutes(15)); db.Executions.Add(e); await db.SaveChangesAsync(ct);
  if(!connections.TryGet(node.Id,out var cid)||cid is null){e.SetStatus(ExecutionStatus.Failed,"NodeAgent 未连接。");slot.Release();await db.SaveChangesAsync(ct);return Conflict(new{message="NodeAgent 未连接。"});}
  await hub.Clients.Client(cid).ExecuteAsync(new ExecutionCommand(e.Id,task.Id,item.Id,task.WorkflowId,task.WorkflowVersion,node.Id,slot.Id,v.DefinitionJson,new Dictionary<string,string?>()));
  return Accepted(new{e.Id,e.Status});
 }
}
public sealed record DispatchRequest(Guid TaskItemId, Guid NodeId, Guid WorkerSlotId);
