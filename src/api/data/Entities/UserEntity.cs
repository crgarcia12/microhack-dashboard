using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Data.Entities;

[Table("users")]
public class UserEntity
{
    [Key]
    [Column("username")]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Column("password")]
    [MaxLength(200)]
    public string Password { get; set; } = string.Empty;

    [Column("role")]
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty;

    [Column("team_name")]
    [MaxLength(100)]
    public string? TeamName { get; set; }

    [ForeignKey(nameof(TeamName))]
    public TeamEntity? Team { get; set; }
}
