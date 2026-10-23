using Microsoft.EntityFrameworkCore;
using SportsClubHub.Domain.Entities;

namespace SportsClubHub.Web.Data;

/// <summary>
/// The Entity Framework Core "unit of work": one DbSet per table, plus the relationship rules.
/// Simple column rules (required, max length) live on the entity classes as data annotations.
/// Anything annotations cannot express (composite keys, unique indexes, delete behaviour)
/// is configured here.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<ClubSport> ClubSports => Set<ClubSport>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueSport> VenueSports => Set<VenueSport>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<SportInterest> SportInterests => Set<SportInterest>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Join entities: the pair of ids is the key, which prevents duplicate rows. ---
        modelBuilder.Entity<ClubSport>().HasKey(cs => new { cs.ClubId, cs.SportId });
        modelBuilder.Entity<VenueSport>().HasKey(vs => new { vs.VenueId, vs.SportId });
        modelBuilder.Entity<SportInterest>().HasKey(si => new { si.ParticipantId, si.SportId });

        // --- Unique values ---
        modelBuilder.Entity<Sport>().HasIndex(s => s.Name).IsUnique();
        modelBuilder.Entity<Participant>().HasIndex(p => p.Email).IsUnique();
        modelBuilder.Entity<AdminUser>().HasIndex(a => a.Email).IsUnique();
        modelBuilder.Entity<Club>().HasIndex(c => new { c.Name, c.Location }).IsUnique();

        // --- Relationships ---
        // Deleting a club or venue removes its join rows. Sports and clubs that have teams cannot
        // be deleted (Restrict); the application deactivates them instead.
        modelBuilder.Entity<ClubSport>()
            .HasOne(cs => cs.Club).WithMany(c => c.ClubSports)
            .HasForeignKey(cs => cs.ClubId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ClubSport>()
            .HasOne(cs => cs.Sport).WithMany(s => s.ClubSports)
            .HasForeignKey(cs => cs.SportId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<VenueSport>()
            .HasOne(vs => vs.Venue).WithMany(v => v.VenueSports)
            .HasForeignKey(vs => vs.VenueId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VenueSport>()
            .HasOne(vs => vs.Sport).WithMany(s => s.VenueSports)
            .HasForeignKey(vs => vs.SportId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Team>()
            .HasOne(t => t.Club).WithMany(c => c.Teams)
            .HasForeignKey(t => t.ClubId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Team>()
            .HasOne(t => t.Sport).WithMany(s => s.Teams)
            .HasForeignKey(t => t.SportId).OnDelete(DeleteBehavior.Restrict);

        // If a participant asks for their data to be erased, their interest rows go with them.
        modelBuilder.Entity<SportInterest>()
            .HasOne(si => si.Participant).WithMany(p => p.SportInterests)
            .HasForeignKey(si => si.ParticipantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SportInterest>()
            .HasOne(si => si.Sport).WithMany(s => s.SportInterests)
            .HasForeignKey(si => si.SportId).OnDelete(DeleteBehavior.Restrict);

        // --- Store enums as readable text instead of numbers ---
        modelBuilder.Entity<Sport>().Property(s => s.Category).HasConversion<string>();
        modelBuilder.Entity<Club>().Property(c => c.VerificationStatus).HasConversion<string>();
        modelBuilder.Entity<Team>().Property(t => t.CompetitionLevel).HasConversion<string>();
        modelBuilder.Entity<Venue>().Property(v => v.VenueType).HasConversion<string>();
    }
}
