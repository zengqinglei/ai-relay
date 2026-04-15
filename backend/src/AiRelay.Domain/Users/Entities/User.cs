using Leistd.Ddd.Domain.Entities.Auditing;

namespace AiRelay.Domain.Users.Entities;

public class User : FullAuditedEntity<Guid>
{
    /// <summary>
    /// 用户名（唯一）
    /// </summary>
    public string Username { get; private set; }

    /// <summary>
    /// 邮箱（唯一）
    /// </summary>
    public string Email { get; private set; }

    /// <summary>
    /// 邮箱是否已验证
    /// </summary>
    public bool EmailConfirmed { get; private set; }

    /// <summary>
    /// 密码哈希（可为空，OAuth 用户无密码）
    /// </summary>
    public string? PasswordHash { get; private set; }

    /// <summary>
    /// 手机号
    /// </summary>
    public string? PhoneNumber { get; private set; }

    /// <summary>
    /// 手机号是否已验证
    /// </summary>
    public bool PhoneNumberConfirmed { get; private set; }

    /// <summary>
    /// 头像
    /// </summary>
    public string? Avatar { get; private set; }

    /// <summary>
    /// 昵称
    /// </summary>
    public string? Nickname { get; private set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// 是否锁定
    /// </summary>
    public bool IsLocked { get; private set; }

    /// <summary>
    /// 锁定截止时间
    /// </summary>
    public DateTime? LockoutEnd { get; private set; }

    /// <summary>
    /// 访问失败次数
    /// </summary>
    public int AccessFailedCount { get; private set; }

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginTime { get; private set; }

    /// <summary>
    /// 最后登录 IP
    /// </summary>
    public string? LastLoginIp { get; private set; }

    private User()
    {
        Username = null!;
        Email = null!;
    }

    public User(string username, string email, string? passwordHash = null, string? nickname = null)
    {
        Id = Guid.CreateVersion7();
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Nickname = nickname ?? username;
    }

    public void Update(string? nickname, string? phoneNumber, string? avatar)
    {
        Nickname = nickname;
        PhoneNumber = phoneNumber;
        Avatar = avatar;
    }

    public void UpdateProfile(string username, string email, string? nickname, string? phoneNumber, string? avatar)
    {
        Username = username;
        Email = email;
        Nickname = nickname;
        PhoneNumber = phoneNumber;
        Avatar = avatar;
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
    }

    public void ConfirmEmail()
    {
        EmailConfirmed = true;
    }

    public void ConfirmPhoneNumber()
    {
        PhoneNumberConfirmed = true;
    }

    public void Enable()
    {
        IsActive = true;
    }

    public void Disable()
    {
        IsActive = false;
    }

    public void Lock(DateTime? lockoutEnd = null)
    {
        IsLocked = true;
        LockoutEnd = lockoutEnd;
    }

    public void Unlock()
    {
        IsLocked = false;
        LockoutEnd = null;
        AccessFailedCount = 0;
    }

    public void RecordAccessFailed()
    {
        AccessFailedCount++;
    }

    public void RecordLoginSuccess(string? ip = null)
    {
        LastLoginTime = DateTime.UtcNow;
        LastLoginIp = ip;
        AccessFailedCount = 0;
    }

    public bool IsLockedOut()
    {
        return IsLocked && (!LockoutEnd.HasValue || LockoutEnd.Value > DateTime.UtcNow);
    }
}
