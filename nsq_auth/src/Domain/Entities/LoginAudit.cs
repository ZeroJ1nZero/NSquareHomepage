namespace Domain.Entities;

public class LoginAudit
{
    public long Id { get; set; }
    public required string UserName { get; set; }
    public required string IpAddress { get; set; }
    public bool Succeeded { get; set; }
    public DateTime AttemptedAtUtc { get; set; }
}
