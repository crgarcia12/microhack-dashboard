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

    [Column("is_microhack")]
    public bool IsMicrohack { get; set; } = false;

    [Column("microhack_id")]
    [MaxLength(100)]
    public string? MicrohackId { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    [Column("schedule_start_utc")]
    public DateTime? ScheduleStartUtc { get; set; }

    [Column("schedule_end_utc")]
    public DateTime? ScheduleEndUtc { get; set; }

    [Column("environment_provisioning_utc")]
    public DateTime? EnvironmentProvisioningUtc { get; set; }

    [Column("time_zone")]
    [MaxLength(100)]
    public string? TimeZone { get; set; }

    [Column("content_path")]
    [MaxLength(300)]
    public string? ContentPath { get; set; }

    [Column("environment_reference")]
    [MaxLength(300)]
    public string? EnvironmentReference { get; set; }
}
