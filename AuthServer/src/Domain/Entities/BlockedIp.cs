namespace Domain.Entities;

/// <summary>차단된 IP 엔티티 (MariaDB 영속화: BlockedIps 테이블). 미들웨어가 매 요청 검사한다 (캐시 경유).</summary>
public class BlockedIp
{
    public int Id { get; set; }
    public required string IpAddress { get; set; }
    public string? Reason { get; set; }
    public DateTime BlockedAtUtc { get; set; }
}
