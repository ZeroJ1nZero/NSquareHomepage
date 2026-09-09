namespace Domain.Entities;

/// <summary>
/// OIDC Refresh Token 영속화 및 회전(Rotation)/폐기(Revocation) 관리 엔티티 (MariaDB 영속화: RefreshTokens 테이블)
/// </summary>
public class RefreshToken
{
    public long Id { get; set; }

    /// <summary>
    /// Refresh Token의 SHA-256 암호학적 해시값 (평문은 DB에 저장하지 않음)
    /// </summary>
    public string RefreshTokenHash { get; set; } = string.Empty;

    /// <summary>
    /// 발급 대상 사용자 식별자 (User ID)
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// (하위 호환용 Subject 별칭)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Subject
    {
        get => UserId;
        set => UserId = value;
    }

    /// <summary>
    /// 발급 대상 사용자 이메일
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// 서비스 식별자 (예: company-homepage)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 허용된 권한 범위 (Scopes)
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// 발급 일시 (UTC)
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 만료 일시 (기본 14일 수명)
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(14);

    /// <summary>
    /// 강제 로그아웃 또는 회전으로 인한 폐기 여부
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    /// 폐기 일시
    /// </summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>
    /// Token Rotation 시 교체된 신규 토큰의 해시값
    /// </summary>
    public string? ReplacedByTokenHash { get; set; }
}
