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
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        private readonly ICurrentUserService? _currentUserService;

        // ✅ MAIN CONSTRUCTOR - Used by DI Container
        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUserService)
            : base(options)
        {
            _currentUserService = currentUserService;
            System.Diagnostics.Debug.WriteLine($"🔧 AppDbContext created with ICurrentUserService: {_currentUserService != null}");
            System.Diagnostics.Debug.WriteLine($"🔧 Current User: {_currentUserService?.CurrentUser?.Username ?? "NULL"}");
        }

        // ✅ FALLBACK CONSTRUCTOR - For design-time/migrations only
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // design-time / tooling only

            System.Diagnostics.Debug.WriteLine($"🔧 AppDbContext created WITHOUT ICurrentUserService (design-time)");
        }

        // ✅ PARAMETERLESS CONSTRUCTOR - For design-time operations only
        public AppDbContext()
        {
            System.Diagnostics.Debug.WriteLine($"🔧 AppDbContext created with parameterless constructor");

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

                // Configure foreign key relationships
                entity.HasOne(e => e.CreatedByUser)
                    .WithMany(u => u.ScoreEntriesCreated)
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.UpdatedByUser)
                    .WithMany(u => u.ScoreEntriesUpdated)
                    .HasForeignKey(e => e.UpdatedByUserId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Create indexes for better performance
                entity.HasIndex(e => e.PatientId).HasDatabaseName("IX_ScoreEntry_PatientId");
                entity.HasIndex(e => e.Date).HasDatabaseName("IX_ScoreEntry_Date");
                entity.HasIndex(e => new { e.PatientId, e.Date }).HasDatabaseName("IX_ScoreEntry_PatientId_Date");
                entity.HasIndex(e => e.CreatedByUserId).HasDatabaseName("IX_ScoreEntry_CreatedByUserId");
                entity.HasIndex(e => e.UpdatedByUserId).HasDatabaseName("IX_ScoreEntry_UpdatedByUserId");

                entity.ToTable("ScoreEntries");
            });

            // Configure AuditLog entity
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PatientId).HasMaxLength(100);
                entity.Property(e => e.IPAddress).HasMaxLength(45);
                entity.Property(e => e.Details).HasMaxLength(500);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.UserId);

                entity.ToTable("AuditLogs");
            });

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.FullName).HasMaxLength(100).IsRequired(false);
                entity.Property(e => e.Email).HasMaxLength(100).IsRequired(false);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Salt).HasMaxLength(50).IsRequired(false);
                entity.Property(e => e.Role).HasMaxLength(20).HasDefaultValue("User").IsRequired(false);

                entity.Property(e => e.CreatedAt).IsRequired().HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2");
                entity.Property(e => e.LastLogin).HasColumnType("datetime2");
                entity.Property(e => e.LockedUntil).HasColumnType("datetime2");
                entity.Property(e => e.PasswordResetExpires).HasColumnType("datetime2");

                entity.Property(e => e.CreatedBy).HasMaxLength(50).IsRequired(false);
                entity.Property(e => e.UpdatedBy).HasMaxLength(50).IsRequired(false);

                // Unique constraints
                entity.HasIndex(e => e.Username).IsUnique().HasDatabaseName("IX_User_Username");
                entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("IX_User_Email");

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
                        optionsBuilder.UseSqlServer(connectionString, sqlServerOptions =>
                        {
                            sqlServerOptions.EnableRetryOnFailure(
                                maxRetryCount: 5,
                                maxRetryDelay: TimeSpan.FromSeconds(30),
                                errorNumbersToAdd: null);
                        });
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

        // ✅ ENHANCED: Better debugging and error handling
        private void UpdateAuditFields()
        {
            var currentUser = _currentUserService?.CurrentUser?.Username ?? "System";
            var currentUserId = _currentUserService?.CurrentUser?.Id;

            System.Diagnostics.Debug.WriteLine($"🔧 ===== UpdateAuditFields called =====");
            System.Diagnostics.Debug.WriteLine($"   _currentUserService: {_currentUserService != null}");
            System.Diagnostics.Debug.WriteLine($"   currentUser: '{currentUser}'");
            System.Diagnostics.Debug.WriteLine($"   currentUserId: {currentUserId}");

            foreach (var entry in ChangeTracker.Entries<ScoreEntry>())
            {
                System.Diagnostics.Debug.WriteLine($"🔧 Processing ScoreEntry - State: {entry.State}, PatientId: {entry.Entity.PatientId}");

                switch (entry.State)
                {
                    case EntityState.Added:
                        // ✅ FIXED: Use DateTime.UtcNow consistently
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                        entry.Entity.CreatedBy = currentUser;
                        entry.Entity.CreatedByUserId = currentUserId;
                        System.Diagnostics.Debug.WriteLine($"   ✅ SET CREATE AUDIT: CreatedBy='{entry.Entity.CreatedBy}', CreatedByUserId={entry.Entity.CreatedByUserId}, CreatedAt={entry.Entity.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                        break;

                    case EntityState.Modified:
                        // ✅ FIXED: Use DateTime.UtcNow consistently
                        entry.Entity.UpdatedAt = DateTime.UtcNow;
                        entry.Entity.UpdatedBy = currentUser;
                        entry.Entity.UpdatedByUserId = currentUserId;
                        System.Diagnostics.Debug.WriteLine($"   ✅ SET UPDATE AUDIT: UpdatedBy='{entry.Entity.UpdatedBy}', UpdatedByUserId={entry.Entity.UpdatedByUserId}, UpdatedAt={entry.Entity.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC");

                        // ✅ IMPORTANT: Don't allow modification of CreatedAt, CreatedBy, CreatedByUserId
                        entry.Property(e => e.CreatedAt).IsModified = false;
                        entry.Property(e => e.CreatedBy).IsModified = false;
                        entry.Property(e => e.CreatedByUserId).IsModified = false;
                        break;
                }
            }

            // Handle User entity audit fields
            foreach (var entry in ChangeTracker.Entries<User>())
            {
                System.Diagnostics.Debug.WriteLine($"🔧 Processing User - State: {entry.State}, Username: {entry.Entity.Username}");

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

            System.Diagnostics.Debug.WriteLine($"🔧 ===== UpdateAuditFields completed =====");
        }
    }

    // ✅ FIXED: Move DateTimeExtensions OUTSIDE the AppDbContext class
    public static class DateTimeExtensions
    {
        public static string ToLocalDisplayString(this DateTime utcDateTime)
        {
            return utcDateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static string ToLocalDisplayString(this DateTime? utcDateTime)
        {
            return utcDateTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        }
    }
}