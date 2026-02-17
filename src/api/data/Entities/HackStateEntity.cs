using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Data.Entities;

[Table("hack_state")]
public class HackStateEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; } = 1; // Singleton table, always id=1
    
    [Column("status")]
    [MaxLength(50)]
    public string Status { get; set; } = "not_started";
    
    [Column("started_at")]
    public DateTime? StartedAt { get; set; }
    
    [Column("configured_by")]
    [MaxLength(100)]
    public string? ConfiguredBy { get; set; }
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
