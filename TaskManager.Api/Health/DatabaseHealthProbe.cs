using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;

namespace TaskManager.Api.Health;

public record DatabaseHealthProbeResult(bool CanConnect, int PendingMigrations, string? Error = null);

public interface IDatabaseHealthProbe
{
    Task<DatabaseHealthProbeResult> CheckAsync();
}

/// <summary>
/// Default implementation that probes the EF Core database connection and migration state.
/// </summary>
public class EfDatabaseHealthProbe : IDatabaseHealthProbe
{
    private readonly ApplicationDbContext _db;

    public EfDatabaseHealthProbe(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DatabaseHealthProbeResult> CheckAsync()
        {
            try
            {
                var providerName = _db.Database.ProviderName;
                if (!string.IsNullOrWhiteSpace(providerName) &&
                    providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
                {
                    // In-memory provider is always treated as connected with no migrations.
                    return new DatabaseHealthProbeResult(true, 0, null);
                }

                var canConnect = await _db.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return new DatabaseHealthProbeResult(false, 0, "Unable to connect to the database.");
            }

            var pending = (await _db.Database.GetPendingMigrationsAsync()).Count();
            return new DatabaseHealthProbeResult(true, pending, pending > 0 ? "Pending migrations exist." : null);
        }
        catch (Exception ex)
        {
            return new DatabaseHealthProbeResult(false, 0, ex.Message);
        }
    }
}
