using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Data.Entities;

[Table("team_progress")]
public class TeamProgressEntity
{
    [Key]
    [Column("team_id")]
    [MaxLength(100)]
    public string TeamId { get; set; } = string.Empty;

    [Column("current_step")]
    public int CurrentStep { get; set; } = 1;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
