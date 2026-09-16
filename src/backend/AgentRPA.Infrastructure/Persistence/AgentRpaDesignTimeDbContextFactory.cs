using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgentRPA.Infrastructure.Persistence;

/// <summary>
/// EF Core 迁移设计时上下文工厂。
///
/// 通过固定的 SQLite 设计时连接串保证 `dotnet ef migrations add` 不依赖 API 启动过程，
/// 后续新增迁移时可直接基于当前领域模型生成数据库变更脚本。
/// </summary>
public sealed class AgentRpaDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AgentRpaDbContext>
{
    public AgentRpaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AgentRpaDbContext>()
            .UseSqlite("Data Source=agentrpa.design.db")
            .Options;

        return new AgentRpaDbContext(options);
    }
}
