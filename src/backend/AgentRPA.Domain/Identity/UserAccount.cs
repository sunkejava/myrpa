using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.Identity;

/// <summary>平台登录账户。密码仅保存 PBKDF2 哈希，不保存明文。</summary>
public sealed class UserAccount : Entity
{
    private readonly List<UserRole> _roles = [];
    private UserAccount() { }

    public UserAccount(string userName, string displayName, string passwordHash)
    {
        UserName = Normalize(userName);
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? UserName : displayName.Trim();
        PasswordHash = passwordHash;
    }

    public string UserName { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public IReadOnlyCollection<UserRole> Roles => _roles;

    public void SetPasswordHash(string hash) => PasswordHash = hash;
    public void SetEnabled(bool enabled) => Enabled = enabled;
    public void AddRole(Guid roleId)
    {
        if (_roles.All(x => x.RoleId != roleId)) _roles.Add(new UserRole(Id, roleId));
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}

public sealed class Role : Entity
{
    private Role() { }
    public Role(string name, string displayName)
    {
        Name = name.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Name : displayName.Trim();
    }
    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public void SetEnabled(bool enabled) => Enabled = enabled;
}

public sealed class UserRole
{
    private UserRole() { }
    public UserRole(Guid userAccountId, Guid roleId) { UserAccountId = userAccountId; RoleId = roleId; }
    public Guid UserAccountId { get; private set; }
    public Guid RoleId { get; private set; }
}

/// <summary>角色继承的城市/系统/功能/动作权限。</summary>
public sealed class RoleAccessPolicy : Entity
{
    private RoleAccessPolicy() { }
    public RoleAccessPolicy(Guid roleId, Guid cityId, Guid systemId, Guid functionId, string action)
    {
        RoleId = roleId; CityId = cityId; SystemId = systemId; FunctionId = functionId; Action = action.Trim();
    }
    public Guid RoleId { get; private set; }
    public Guid CityId { get; private set; }
    public Guid SystemId { get; private set; }
    public Guid FunctionId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public void SetEnabled(bool enabled) => Enabled = enabled;
}