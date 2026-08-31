namespace Domain.Entities;

/// <summary>
/// 로그인 시도 및 접속 로그 엔티티 (MariaDB 영속화: LoginLogs 테이블)
/// </summary>
public class LoginLog
{
    public long Id { get; set; }
    public required string LoginId { get; set; }
    public required string IpAddress { get; set; }
    public bool Succeeded { get; set; }
    public DateTime AttemptedAtUtc { get; set; }
}
