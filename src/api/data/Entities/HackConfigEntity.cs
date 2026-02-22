using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Data.Entities;

[Table("hack_config")]
public class HackConfigEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; } = 1; // Singleton table, always id=1
    
    [Column("content_path")]
    [MaxLength(500)]
    public string? ContentPath { get; set; }
    
    [Column("config_json")]
    public string ConfigJson { get; set; } = "{}";
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
