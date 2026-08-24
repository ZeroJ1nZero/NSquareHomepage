namespace Domain.Entities;

/// <summary>
/// OIDC 인가 코드 및 PKCE Code Challenge 해시 저장 엔티티 (MariaDB 영속화)
/// </summary>
public class IssuedAuthorizationCode
{
    public long Id { get; set; }

    /// <summary>
    /// 발급된 인가 코드 원문 (또는 토큰 키)
    /// </summary>
    public string AuthorizationCode { get; set; } = string.Empty;

    /// <summary>
    /// 인가 코드의 SHA-256 암호학적 해시값
    /// </summary>
    public string AuthorizationCodeHash { get; set; } = string.Empty;

    /// <summary>
    /// 클라이언트가 제출한 PKCE Code Challenge 해시키
    /// </summary>
    public string CodeChallenge { get; set; } = string.Empty;

    /// <summary>
    /// PKCE Code Challenge의 SHA-256 해시값 (DB 이중 해시 저장)
    /// </summary>
    public string CodeChallengeHash { get; set; } = string.Empty;

    /// <summary>
    /// Zero-Trust DB 무결성 결합 해시: AuthorizationCode와 CodeChallenge를 한 묶음으로 묶어 계산한 SHA-256 해시값
    /// SHA256(AuthorizationCode + ":" + CodeChallenge)
    /// </summary>
    public string CombinedBindingHash { get; set; } = string.Empty;

    /// <summary>
    /// PKCE 해시 방식 (S256)
    /// </summary>
    public string CodeChallengeMethod { get; set; } = "S256";

    /// <summary>
    /// 서비스 식별자 (예: company-homepage)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 인가 완료 후 복귀할 콜백 URL
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// 인증된 사용자 식별자 (User Subject / ID)
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 인증된 사용자 이메일
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// CSRF 방어용 state 값
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// 요청된 권한 범위 (Scopes)
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// 인가 코드 생성 일시 (UTC)
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 인가 코드 만료 일시 (기본 5분 수명)
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(5);

    /// <summary>
    /// 토큰 교환 완료(사용) 여부
    /// </summary>
    public bool IsRedeemed { get; set; } = false;

    /// <summary>
    /// 토큰 교환 완료 일시
    /// </summary>
    public DateTime? RedeemedAtUtc { get; set; }
}
