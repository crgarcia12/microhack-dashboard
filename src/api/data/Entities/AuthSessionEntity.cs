using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Data.Entities;

[Table("auth_sessions")]
public class AuthSessionEntity
{
    [Key]
    [Column("session_id")]
    [MaxLength(64)]
    public string SessionId { get; set; } = string.Empty;

    [Column("username")]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Column("role")]
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty;

    [Column("team")]
    [MaxLength(100)]
    public string? Team { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
