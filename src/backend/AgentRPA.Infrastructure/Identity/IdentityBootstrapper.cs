using AgentRPA.Application.Identity;
using AgentRPA.Domain.Identity;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AgentRPA.Infrastructure.Identity;

/// <summary>首次启动时按显式配置创建管理员账户；未配置密码时不会创建默认账户。</summary>
public static class IdentityBootstrapper
{
    public static async Task SeedAsync(AgentRpaDbContext db, IPasswordHasher hasher, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (await db.UserAccounts.AnyAsync(cancellationToken)) return;
        var password = configuration["AgentRPA:Bootstrap:AdminPassword"];
        if (string.IsNullOrEmpty(password)) return;
        if (password.Length < 10) throw new InvalidOperationException("AgentRPA:Bootstrap:AdminPassword 至少需要 10 个字符。");

        var role = await db.Roles.SingleOrDefaultAsync(x => x.Name == "Admin", cancellationToken);
        if (role is null) { role = new Role("Admin", "系统管理员"); db.Roles.Add(role); }
        var user = new UserAccount("admin", "系统管理员", hasher.Hash(password));
        user.AddRole(role.Id);
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }
}
