using AgentRPA.Domain.Permission;
using AgentRPA.Domain.Resources;
using AgentRPA.Domain.Workflow;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Seeding;

/// <summary>可重复执行的公共模型下载资源；已有资源、授权与版本保持管理员维护的状态。</summary>
public static class ModelDownloadSeedData
{
    public static async Task SeedAsync(AgentRpaDbContext db, CancellationToken ct = default)
    {
        var city = await db.Cities.SingleAsync(x => x.Code == "CN-BJ", ct);
        var system = await db.BusinessSystems.SingleOrDefaultAsync(x => x.CityId == city.Id && x.Code == "MODEL-HUB", ct);
        if (system is null) { system = new BusinessSystem(city.Id, "MODEL-HUB", "公共模型资源 · ModelScope", "https://modelscope.cn"); db.BusinessSystems.Add(system); }
        var function = await db.BusinessFunctions.SingleOrDefaultAsync(x => x.SystemId == system.Id && x.Code == "MODEL-DOWNLOAD", ct);
        if (function is null) { function = new BusinessFunction(system.Id, "MODEL-DOWNLOAD", "模型搜索与 GGUF 下载"); db.BusinessFunctions.Add(function); }
        if (!await db.Workflows.AnyAsync(x => x.BusinessFunctionId == function.Id && x.Name == "模型搜索与 GGUF 下载", ct))
        {
            await using var source = typeof(ModelDownloadSeedData).Assembly.GetManifestResourceStream("ModelDownload.Workflow.json")
                ?? throw new InvalidOperationException("缺少模型下载默认流程。");
            using var reader = new StreamReader(source);
            var workflow = new Workflow(function.Id, "模型搜索与 GGUF 下载", "Bing → 魔搭 → 精确匹配 GGUF → 流式下载校验；支持 Excel、日志和视频。");
            var version = new WorkflowVersion(workflow.Id, 1, await reader.ReadToEndAsync(ct));
            version.Publish(); workflow.Publish();
            db.Workflows.Add(workflow); db.WorkflowVersions.Add(version);
        }
        var admin = await db.UserAccounts.SingleOrDefaultAsync(x => x.UserName == "admin", ct);
        if (admin is not null)
            foreach (var action in new[] { "Execute", "Download" })
                if (!await db.AccessPolicies.AnyAsync(x => x.SubjectId == admin.Id && x.CityId == city.Id && x.SystemId == system.Id && x.FunctionId == function.Id && x.Action == action, ct))
                    db.AccessPolicies.Add(new AccessPolicy(admin.Id, city.Id, system.Id, function.Id, action));
        await db.SaveChangesAsync(ct);
    }
}
