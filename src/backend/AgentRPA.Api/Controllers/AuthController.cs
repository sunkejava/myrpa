using AgentRPA.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentRPA.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(IIdentityService identity, IJwtTokenService tokens) : ControllerBase
{
    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { message = "用户名和密码不能为空。" });
        var user = await identity.AuthenticateAsync(request.UserName, request.Password, ct);
        if (user is null) return Unauthorized(new { message = "用户名或密码错误。" });
        return Ok(new { accessToken = tokens.CreateToken(user), user.UserId, user.UserName, user.DisplayName, user.Roles });
    }

    [Authorize, HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
        userName = User.Identity?.Name,
        roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(x => x.Value).Distinct().ToArray()
    });
}

public sealed record LoginRequest(string UserName, string Password);
