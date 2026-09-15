using AgentRPA.Domain.Identity;

namespace AgentRPA.Application.Identity;

public sealed record LoginResult(Guid UserId, string UserName, string DisplayName, IReadOnlyList<string> Roles);

public interface IIdentityService
{
    Task<LoginResult?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken);
    Task<UserAccount> CreateUserAsync(string userName, string displayName, string password, CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);
}

public interface IJwtTokenService
{
    string CreateToken(LoginResult user);
}
