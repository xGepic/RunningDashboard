using Microsoft.EntityFrameworkCore;

namespace RunningDashboard.Api.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<StravaConnection> StravaConnections => Set<StravaConnection>();

    public DbSet<Run> Runs => Set<Run>();

    public DbSet<SyncState> SyncStates => Set<SyncState>();

    public Task<bool> IsStravaConnectedAsync(CancellationToken cancellationToken) =>
        StravaConnections.AnyAsync(cancellationToken);

    /// <summary>
    /// The single sync state row, created (and tracked) on first use.
    /// </summary>
    public async Task<SyncState> GetSyncStateAsync(CancellationToken cancellationToken)
    {
        var state = await SyncStates.SingleOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new SyncState();
            SyncStates.Add(state);
        }

        return state;
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<StravaConnection>(connection =>
        {
            connection.Property(c => c.Id).ValueGeneratedNever();
            connection.Property(c => c.FirstName).HasMaxLength(200);
            connection.Property(c => c.LastName).HasMaxLength(200);
        });

        model.Entity<Run>(run =>
        {
            run.Property(r => r.Id).ValueGeneratedNever();
            run.Property(r => r.SportType).HasMaxLength(50);
            run.Property(r => r.ShoeId).HasMaxLength(50);
            run.HasIndex(r => r.StartUtc);
            run.HasMany(r => r.BestEfforts).WithOne().HasForeignKey(e => e.RunId).OnDelete(DeleteBehavior.Cascade);
            run.HasMany(r => r.Splits).WithOne().HasForeignKey(s => s.RunId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<BestEffort>(effort => effort.Property(e => e.Name).HasMaxLength(50));

        model.Entity<Split>(split => split.HasKey(s => new { s.RunId, s.Index }));

        model.Entity<SyncState>(state => state.Property(s => s.Id).ValueGeneratedNever());
    }
}
