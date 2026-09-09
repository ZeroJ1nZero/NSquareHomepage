namespace Domain.Entities;

/// <summary>
/// OIDC 인가 코드 및 PKCE Code Challenge 해시 저장 엔티티 (MariaDB 영속화: AuthorizationCodes 테이블)
/// </summary>
public class AuthorizationCode
{
    public long Id { get; set; }

    /// <summary>
    /// 인가 코드의 SHA-256 암호학적 해시값 (평문은 DB에 저장하지 않음)
    /// </summary>
    public string AuthorizationCodeHash { get; set; } = string.Empty;

    /// <summary>
    /// PKCE Code Challenge의 SHA-256 해시값 (평문은 DB에 저장하지 않음)
    /// </summary>
    public string CodeChallengeHash { get; set; } = string.Empty;

    /// <summary>
    /// 서비스 식별자 (예: company-homepage)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 인가 완료 후 복귀할 콜백 URL
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// 인증된 사용자 식별자 (User ID)
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
    /// 인증된 사용자 이메일
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// 요청된 권한 범위 (Scopes)
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// 인가 코드 생성 일시 (UTC)
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 인가 코드 만료 일시 (기본 1분 수명)
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(1);

    /// <summary>
    /// 토큰 교환 완료(사용) 여부 (1회용 소진)
    /// </summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    /// 토큰 교환 완료 일시
    /// </summary>
    public DateTime? UsedAtUtc { get; set; }

    /// <summary>
    /// (하위 호환용 별칭)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsRedeemed
    {
        get => IsUsed;
        set => IsUsed = value;
    }

    /// <summary>
    /// (하위 호환용 별칭)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime? RedeemedAtUtc
    {
        get => UsedAtUtc;
        set => UsedAtUtc = value;
    }
}
