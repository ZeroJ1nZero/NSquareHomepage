namespace Domain.Entities;

public enum UserRole
{
    Customer = 0, // 고객 (가입 기본값)
    Employee = 1, // 직원
    Admin = 2,    // 관리자
}

public class User
{
    public long Id { get; set; }
    public required string Email { get; set; }
    public required string UserName { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
}
