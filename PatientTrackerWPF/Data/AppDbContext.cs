using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PatientTrackerWPF.Models;
using PatientTrackerWPF.Services;
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

        private readonly ICurrentUserService? _currentUserService;

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUserService)
            : base(options)
        {
            _currentUserService = currentUserService;
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
                entity.HasKey(e => e.Id);

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

                // Configure foreign key relationships - FIXED
                entity.HasOne(e => e.CreatedByUser)
                    .WithMany(u => u.ScoreEntriesCreated)
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.UpdatedByUser)
                    .WithMany(u => u.ScoreEntriesUpdated)
                    .HasForeignKey(e => e.UpdatedByUserId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Create indexes for better performance
                entity.HasIndex(e => e.PatientId)
                    .HasDatabaseName("IX_ScoreEntry_PatientId");

                entity.HasIndex(e => e.Date)
                    .HasDatabaseName("IX_ScoreEntry_Date");

                entity.HasIndex(e => new { e.PatientId, e.Date })
                    .HasDatabaseName("IX_ScoreEntry_PatientId_Date");

                entity.HasIndex(e => e.CreatedByUserId)
                    .HasDatabaseName("IX_ScoreEntry_CreatedByUserId");

                entity.HasIndex(e => e.UpdatedByUserId)
                    .HasDatabaseName("IX_ScoreEntry_UpdatedByUserId");

                entity.ToTable("ScoreEntries");
            });

            // FIXED: Configure User entity to match nullable model
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Username)
                    .IsRequired()
                    .HasMaxLength(50);

                // FIXED: Made nullable to match model
                entity.Property(e => e.FullName)
                    .HasMaxLength(100)
                    .IsRequired(false);

                // FIXED: Made nullable to match model
                entity.Property(e => e.Email)
                    .HasMaxLength(100)
                    .IsRequired(false);

                entity.Property(e => e.PasswordHash)
                    .IsRequired()
                    .HasMaxLength(255);

                // FIXED: Made nullable for BCrypt
                entity.Property(e => e.Salt)
                    .HasMaxLength(50)
                    .IsRequired(false);

                // FIXED: Made nullable to match model
                entity.Property(e => e.Role)
                    .HasMaxLength(20)
                    .HasDefaultValue("User")
                    .IsRequired(false);

                entity.Property(e => e.CreatedAt)
                    .IsRequired()
                    .HasColumnType("datetime2")
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime2");

                entity.Property(e => e.LastLogin)
                    .HasColumnType("datetime2");

                entity.Property(e => e.LockedUntil)
                    .HasColumnType("datetime2");

                entity.Property(e => e.PasswordResetExpires)
                    .HasColumnType("datetime2");

                // FIXED: Made nullable
                entity.Property(e => e.CreatedBy)
                    .HasMaxLength(50)
                    .IsRequired(false);

                entity.Property(e => e.UpdatedBy)
                    .HasMaxLength(50)
                    .IsRequired(false);

                // Unique constraints
                entity.HasIndex(e => e.Username)
                    .IsUnique()
                    .HasDatabaseName("IX_User_Username");

                entity.HasIndex(e => e.Email)
                    .IsUnique()
                    .HasDatabaseName("IX_User_Email");

                entity.ToTable("Users");
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
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

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateAuditFields();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateAuditFields()
        {
            var currentUser = _currentUserService?.CurrentUser?.Username ?? "System";
            var currentUserId = _currentUserService?.CurrentUser?.Id;

            foreach (var entry in ChangeTracker.Entries<ScoreEntry>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                        entry.Entity.CreatedBy = currentUser;
                        entry.Entity.CreatedByUserId = currentUserId;
                        break;

                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = DateTime.UtcNow;
                        entry.Entity.UpdatedBy = currentUser;
                        entry.Entity.UpdatedByUserId = currentUserId;
                        break;
                }
            }

            foreach (var entry in ChangeTracker.Entries<User>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy ??= currentUser;
                }

                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = currentUser;
                }
            }
        }
    }
}