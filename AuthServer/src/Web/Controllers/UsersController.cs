using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Application.UseCases;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Web.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
[Tags("사용자 및 계정 관리 (User Management)")]
public class UsersController(
    RegisterUserUseCase registerUseCase,
    AppDbContext db) : ControllerBase
{
    /// <summary>
    /// 신규 사용자 등록 (회원가입)
    /// </summary>
    /// <remarks>
    /// Swagger UI에서 직접 사용자 계정을 생성합니다.
    /// - **Email**: 로그인 ID (예: user@company.local, user@domain.com 등)
    /// - **UserName**: 표시 이름 (예: 홍길동, 관리자)
    /// - **Password**: 비밀번호 (최소 6자 이상)
    /// - **Role**: 사용자 권한 (Customer, Employee, Admin)
    /// </remarks>
    /// <param name="request">가입 정보</param>
    /// <param name="ct">취소 토큰</param>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request, CancellationToken ct)
    {
        var result = await registerUseCase.ExecuteAsync(
            request.Email,
            request.UserName,
            request.Password,
            request.Role,
            ct);

        if (!result.Succeeded)
        {
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Message = "사용자 등록에 실패했습니다.",
                Errors = result.Errors
            });
        }

        return Ok(new UserResponseDto
        {
            Success = true,
            Message = "사용자가 성공적으로 등록되었습니다.",
            UserId = result.UserId,
            Email = request.Email.Trim(),
            UserName = request.UserName.Trim(),
            Role = request.Role,
            RoleName = request.Role.ToString()
        });
    }

    /// <summary>
    /// 전체 사용자 목록 조회
    /// </summary>
    /// <remarks>
    /// 현재 DB에 등록된 모든 사용자 계정 목록을 조회합니다.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(List<UserSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers(CancellationToken ct)
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => new UserSummaryDto
            {
                Id = u.Id,
                Email = u.Email,
                UserName = u.UserName,
                Role = u.Role,
                RoleName = u.Role.ToString()
            })
            .ToListAsync(ct);

        return Ok(users);
    }

    /// <summary>
    /// 특정 사용자 상세 조회
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(long id, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null)
            return NotFound(new { message = $"ID {id}인 사용자를 찾을 수 없습니다." });

        return Ok(new UserSummaryDto
        {
            Id = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            Role = user.Role,
            RoleName = user.Role.ToString()
        });
    }

    /// <summary>
    /// 사용자 역할 변경
    /// </summary>
    /// <remarks>
    /// 사용자의 역할을 변경합니다. (0 = Customer, 1 = Employee, 2 = Admin)
    /// </remarks>
    [HttpPut("{id:long}/role")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeRole(long id, [FromBody] ChangeRoleRequestDto request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Role))
            return BadRequest(new ErrorResponseDto { Success = false, Message = "올바르지 않은 역할입니다." });

        var user = await db.Users.FindAsync([id], ct);
        if (user is null)
            return NotFound(new { message = $"ID {id}인 사용자를 찾을 수 없습니다." });

        user.Role = request.Role;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            message = $"사용자({user.Email})의 역할이 {user.Role}(으)로 성공적으로 변경되었습니다.",
            userId = user.Id,
            newRole = user.Role,
            newRoleName = user.Role.ToString()
        });
    }

    /// <summary>
    /// 사용자 삭제
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(long id, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null)
            return NotFound(new { message = $"ID {id}인 사용자를 찾을 수 없습니다." });

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);

        return Ok(new { success = true, message = $"사용자({user.Email})가 삭제되었습니다." });
    }
}

public class RegisterUserRequest
{
    [Required(ErrorMessage = "이메일은 필수입니다.")]
    [EmailAddress(ErrorMessage = "올바른 이메일 형식(예: user@company.local 또는 user@domain.com)이어야 합니다.")]
    [Example("user@company.local")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "이름은 필수입니다.")]
    [Example("홍길동")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "비밀번호는 필수입니다.")]
    [MinLength(6, ErrorMessage = "비밀번호는 6자 이상이어야 합니다.")]
    [Example("Test1234!")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 사용자 권한: 0 = Customer(고객), 1 = Employee(직원), 2 = Admin(관리자)
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Customer;
}

public class ChangeRoleRequestDto
{
    /// <summary>
    /// 변경할 권한: 0 = Customer(고객), 1 = Employee(직원), 2 = Admin(관리자)
    /// </summary>
    public UserRole Role { get; set; }
}

public class UserSummaryDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
}

public class UserResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
}

public class ErrorResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<string> Errors { get; set; } = [];
}

[AttributeUsage(AttributeTargets.Property)]
internal class ExampleAttribute(string example) : Attribute
{
    public string Value { get; } = example;
}
