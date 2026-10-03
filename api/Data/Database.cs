using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Portfolio.Yard.Api.Data;

/// <summary>What the API is storing data in, and whether it is reachable.</summary>
public sealed class DatabaseStatus
{
    public required string Mode { get; init; }
    public bool Ready { get; set; }
    public string? Error { get; set; }
    public bool Persistent => Mode == Database.SqlServerMode;
}

public static class Database
{
    public const string SqlServerMode = "SQL Server";
    public const string DemoMode = "Demo (SQLite, resets on restart)";

    /// <summary>
    /// SQL Server when ConnectionStrings:Default or DATABASE_URL is set (for example an Azure SQL
    /// Database connection string); otherwise a throwaway SQLite file so the demo runs with no
    /// database configured.
    /// </summary>
    public static DatabaseStatus AddYardDatabase(this IServiceCollection services, IConfiguration config)
    {
        var configured = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(configured)) configured = config["DATABASE_URL"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            // No retrying execution strategy: several writes use explicit transactions.
            services.AddDbContext<YardDbContext>(o => o.UseSqlServer(configured));
            return Register(services, new DatabaseStatus { Mode = SqlServerMode });
        }

        var path = config["Demo:SqlitePath"] ?? Path.Combine(Path.GetTempPath(), "yard-ops-demo.db");
        services.AddDbContext<YardDbContext>(o => o.UseSqlite($"Data Source={path};Default Timeout=10"));
        return Register(services, new DatabaseStatus { Mode = DemoMode });
    }

    private static DatabaseStatus Register(IServiceCollection services, DatabaseStatus status)
    {
        services.AddSingleton(status);
        return status;
    }

    /// <summary>
    /// Brings the schema up to date and seeds demo data into an empty database. Never throws:
    /// if the database is unreachable the API still starts, /health/ready reports why, and
    /// data endpoints answer 503.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var status = scope.ServiceProvider.GetRequiredService<DatabaseStatus>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
        var db = scope.ServiceProvider.GetRequiredService<YardDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();

        try
        {
            if (status.Persistent)
            {
                await db.Database.MigrateAsync(ct);
            }
            else
            {
                await db.Database.EnsureDeletedAsync(ct);
                await db.Database.EnsureCreatedAsync(ct);
            }

            if (!await db.Spots.AnyAsync(ct)) await seeder.SeedAsync(db, ct);
            status.Ready = true;
            status.Error = null;
            logger.LogInformation("Database ready ({Mode}).", status.Mode);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            status.Ready = false;
            status.Error = "The database could not be reached.";
            logger.LogError(ex, "Database initialization failed ({Mode}).", status.Mode);
        }
    }
}
