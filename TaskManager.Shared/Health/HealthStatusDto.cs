using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.Health;

public class HealthStatusDto
{
    [Required]
    [StringLength(100)]
    public string Service { get; set; } = string.Empty;

    [Required]
    [RegularExpression("Healthy|Degraded|Unhealthy", ErrorMessage = "Status must be Healthy, Degraded or Unhealthy.")]
    public string Status { get; set; } = "Healthy";

    [StringLength(2000)]
    public string? Details { get; set; }

    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;

    public IDictionary<string, string>? Metadata { get; set; }
}
