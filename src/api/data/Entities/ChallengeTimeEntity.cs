using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Data.Entities;

[Table("challenge_times")]
public class ChallengeTimeEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("team_name")]
    [MaxLength(100)]
    public string TeamName { get; set; } = string.Empty;

    [Column("challenge_number")]
    public int ChallengeNumber { get; set; }

    [Column("seconds")]
    public int Seconds { get; set; }

    [ForeignKey(nameof(TeamName))]
    public TimerStateEntity? TimerState { get; set; }
}
