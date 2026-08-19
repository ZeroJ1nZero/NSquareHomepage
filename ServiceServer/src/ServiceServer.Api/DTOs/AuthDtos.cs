namespace ServiceServer.Api.DTOs;

/// <summary>
/// 로그인 요청 DTO (인증 서버 계정 검증용)
/// </summary>
/// <param name="Email">로그인 아이디 (이메일, 예: test@company.local)</param>
/// <param name="Password">비밀번호 (예: Test1234!)</param>
public record LoginRequestDto(string Email, string Password);

/// <summary>
/// 로그인 응답 DTO
/// </summary>
public record LoginResponseDto(
    bool Success,
    string Message,
    UserInfoDto? User
);

/// <summary>
/// 인증된 사용자 정보 DTO
/// </summary>
public record UserInfoDto(
    long Id,
    string Email,
    string UserName,
    string Role
);
