using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Data.Entities;

[Table("credential_categories")]
public class CredentialCategoryEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("team_name")]
    [MaxLength(100)]
    public string TeamName { get; set; } = string.Empty;

    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public List<CredentialItemEntity> Credentials { get; set; } = new();
}
