using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Data.Entities;

[Table("timer_states")]
public class TimerStateEntity
{
    [Key]
    [Column("team_name")]
    [MaxLength(100)]
    public string TeamName { get; set; } = string.Empty;

    [Column("manual_timer_status")]
    [MaxLength(20)]
    public string ManualTimerStatus { get; set; } = "stopped";

    [Column("manual_timer_started_at")]
    public DateTime? ManualTimerStartedAt { get; set; }

    [Column("manual_timer_accumulated_seconds")]
    public int ManualTimerAccumulatedSeconds { get; set; }

    [Column("timer_started_at")]
    public DateTime? TimerStartedAt { get; set; }

    public List<ChallengeTimeEntity> ChallengeTimes { get; set; } = new();
}
