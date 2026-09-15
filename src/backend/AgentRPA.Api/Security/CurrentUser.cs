using System.Security.Claims;

namespace AgentRPA.Api.Security;

/// <summary>从已验证 JWT 中取得当前业务主体，禁止 API 直接信任客户端提交的 SubjectId。</summary>
public static class CurrentUser
{
    public static bool TryGetSubjectId(ClaimsPrincipal user, out Guid subjectId)
    {
        subjectId = Guid.Empty;
        if (user.Identity?.IsAuthenticated != true) return false;

        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        return Guid.TryParse(value, out subjectId) && subjectId != Guid.Empty;
    }
}
