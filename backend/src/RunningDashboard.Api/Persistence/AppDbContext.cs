using Microsoft.EntityFrameworkCore;

namespace RunningDashboard.Api.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<StravaConnection> StravaConnections => Set<StravaConnection>();

    public DbSet<Run> Runs => Set<Run>();

    public Task<bool> IsStravaConnectedAsync(CancellationToken cancellationToken) =>
        StravaConnections.AnyAsync(cancellationToken);

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
        });
    }
}
