using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Data.Entities;

[Table("credential_items")]
public class CredentialItemEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("label")]
    [MaxLength(200)]
    public string Label { get; set; } = string.Empty;

    [Column("value")]
    [MaxLength(500)]
    public string Value { get; set; } = string.Empty;

    [ForeignKey(nameof(CategoryId))]
    public CredentialCategoryEntity? Category { get; set; }
}
