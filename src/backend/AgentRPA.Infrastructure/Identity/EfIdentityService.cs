using AgentRPA.Application.Identity;
using AgentRPA.Domain.Identity;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Identity;

public sealed class EfIdentityService(AgentRpaDbContext db, IPasswordHasher passwordHasher) : IIdentityService
{
    public async Task<LoginResult?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var normalized = userName.Trim().ToLowerInvariant();
        var user = await db.UserAccounts.Include(x => x.Roles).SingleOrDefaultAsync(x => x.UserName == normalized, cancellationToken);
        if (user is null || !user.Enabled || !passwordHasher.Verify(password, user.PasswordHash)) return null;

        var roleIds = user.Roles.Select(x => x.RoleId).ToArray();
        var roles = await db.Roles.Where(x => roleIds.Contains(x.Id) && x.Enabled).Select(x => x.Name).ToListAsync(cancellationToken);
        return new LoginResult(user.Id, user.UserName, user.DisplayName, roles);
    }

    public async Task<UserAccount> CreateUserAsync(string userName, string displayName, string password, CancellationToken cancellationToken)
    {
        var normalized = userName.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 128) throw new ArgumentException("用户名无效。");
        if (password.Length < 10) throw new ArgumentException("密码至少需要 10 个字符。");
        if (await db.UserAccounts.AnyAsync(x => x.UserName == normalized, cancellationToken)) throw new InvalidOperationException("用户名已存在。");
        var user = new UserAccount(normalized, displayName, passwordHasher.Hash(password));
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }
}
