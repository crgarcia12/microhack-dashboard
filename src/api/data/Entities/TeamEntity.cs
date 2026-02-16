using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Data.Entities;

[Table("teams")]
public class TeamEntity
{
    [Key]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
