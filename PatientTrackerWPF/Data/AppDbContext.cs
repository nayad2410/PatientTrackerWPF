using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PatientTrackerWPF.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PatientTrackerWPF.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<ScoreEntry> ScoreEntries { get; set; }
        public DbSet<User> Users { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Parameterless constructor for design-time operations (migrations)
        public AppDbContext()
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure ScoreEntry entity
            modelBuilder.Entity<ScoreEntry>(entity =>
            {
                // Primary key
                entity.HasKey(e => e.Id);

                // Configure properties
                entity.Property(e => e.PatientId)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Note)
                    .HasMaxLength(2000)
                    .IsRequired(false);

                entity.Property(e => e.Date)
                    .IsRequired()
                    .HasColumnType("datetime2");

                entity.Property(e => e.CreatedAt)
                    .IsRequired()
                    .HasColumnType("datetime2")
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime2");

                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(100);

                entity.Property(e => e.UpdatedBy)
                    .HasMaxLength(100);

                // Add constraints for score ranges
                entity.HasCheckConstraint("CK_ScoreEntry_PHQ9_Range", "[PHQ9] >= 0 AND [PHQ9] <= 100");
                entity.HasCheckConstraint("CK_ScoreEntry_GAD7_Range", "[GAD7] >= 0 AND [GAD7] <= 100");
                entity.HasCheckConstraint("CK_ScoreEntry_PCL5_Range", "[PCL5] >= 0 AND [PCL5] <= 100");
                entity.HasCheckConstraint("CK_ScoreEntry_BDI2_Range", "[BDI2] >= 0 AND [BDI2] <= 100");
                entity.HasCheckConstraint("CK_ScoreEntry_YBOCS_Range", "[YBOCS] >= 0 AND [YBOCS] <= 100");

                // Create indexes for better performance
                entity.HasIndex(e => e.PatientId)
                    .HasDatabaseName("IX_ScoreEntry_PatientId");

                entity.HasIndex(e => e.Date)
                    .HasDatabaseName("IX_ScoreEntry_Date");

                entity.HasIndex(e => new { e.PatientId, e.Date })
                    .HasDatabaseName("IX_ScoreEntry_PatientId_Date");

                // Table name
                entity.ToTable("ScoreEntries");
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Only configure if not already configured (for design-time support)
            if (!optionsBuilder.IsConfigured)
            {
                try
                {
                    var config = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false)
                        .Build();

                    var connectionString = config.GetConnectionString("PatientDb");

                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        optionsBuilder.UseSqlServer(connectionString);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error configuring DbContext: {ex.Message}");
                }
            }
        }

        // Override SaveChanges to automatically set audit fields
        public override int SaveChanges()
        {
            UpdateAuditFields();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateAuditFields();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateAuditFields()
        {
            var entries = ChangeTracker.Entries<ScoreEntry>();

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                        entry.Entity.CreatedBy = Environment.UserName;
                        break;

                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = DateTime.UtcNow;
                        entry.Entity.UpdatedBy = Environment.UserName;
                        break;
                }
            }
        }
    }
}