using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace RunningDashboard.Api.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<StravaConnection> StravaConnections => Set<StravaConnection>();

    public DbSet<Run> Runs => Set<Run>();

    public DbSet<Ride> Rides => Set<Ride>();

    public DbSet<Swim> Swims => Set<Swim>();

    public DbSet<SyncState> SyncStates => Set<SyncState>();

    public DbSet<Shoe> Shoes => Set<Shoe>();

    public DbSet<GoalRace> GoalRaces => Set<GoalRace>();

    public DbSet<YearlyGoal> YearlyGoals => Set<YearlyGoal>();

    public DbSet<AthleteSettings> AthleteSettings => Set<AthleteSettings>();

    /// <summary>The Yearly Goal for <paramref name="year"/>; none if none is set or it is for another year.</summary>
    public Task<YearlyGoal?> GetYearlyGoalAsync(int year, CancellationToken cancellationToken) =>
        YearlyGoals.SingleOrDefaultAsync(g => g.Year == year, cancellationToken);

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

        model.Entity<Ride>(ride =>
        {
            ride.Property(r => r.Id).ValueGeneratedNever();
            ride.Property(r => r.SportType).HasMaxLength(50);
            ride.HasIndex(r => r.StartUtc);
        });

        model.Entity<Swim>(swim =>
        {
            swim.Property(s => s.Id).ValueGeneratedNever();
            swim.Property(s => s.SportType).HasMaxLength(50);
            swim.HasIndex(s => s.StartUtc);
        });

        model.Entity<BestEffort>(effort => effort.Property(e => e.Name).HasMaxLength(50));

        model.Entity<Split>(split => split.HasKey(s => new { s.RunId, s.Index }));

        model.Entity<SyncState>(state => state.Property(s => s.Id).ValueGeneratedNever());

        model.Entity<YearlyGoal>(goal => goal.Property(g => g.Id).ValueGeneratedNever());

        model.Entity<AthleteSettings>(settings =>
        {
            settings.Property(s => s.Id).ValueGeneratedNever();
            settings.Property(s => s.DistanceUnit).HasMaxLength(2);
        });

        model.Entity<Shoe>(shoe =>
        {
            shoe.Property(s => s.Id).HasMaxLength(50);
            shoe.Property(s => s.Name).HasMaxLength(200);
        });

        model.Entity<GoalRace>(race =>
        {
            race.Property(r => r.Name).HasMaxLength(200);
            race.Ignore(r => r.TrainingBlock);
            race.Ignore(r => r.TargetPace);
            // Owned, so a Goal Race always comes with its plan, and its Training Block with the plan's length.
            race.OwnsOne(r => r.TrainingPlan, plan =>
            {
                plan.ToTable("TrainingPlans");
                plan.WithOwner().HasForeignKey("GoalRaceId");
                plan.OwnsMany(p => p.Weeks, week =>
                {
                    week.ToTable("TrainingPlanWeeks");
                    week.WithOwner().HasForeignKey("GoalRaceId");
                    week.HasKey("GoalRaceId", nameof(TrainingPlanWeek.WeeksToRace));
                    week.Property(w => w.WeeksToRace).ValueGeneratedNever();
                    // One JSON column, as when the Running Days were plain weekdays. EF can't map JSON inside an owned type with its own table.
                    week.Property(w => w.RunningDays).HasConversion(
                        days => JsonSerializer.Serialize(days, (JsonSerializerOptions?)null),
                        json => JsonSerializer.Deserialize<List<RunningDay>>(json, (JsonSerializerOptions?)null)!,
                        new ValueComparer<List<RunningDay>>(
                            (a, b) => a!.SequenceEqual(b!),
                            days => days.Aggregate(0, (hash, day) => HashCode.Combine(hash, day)),
                            days => days.ToList()));
                });
            });
        });
    }
}
