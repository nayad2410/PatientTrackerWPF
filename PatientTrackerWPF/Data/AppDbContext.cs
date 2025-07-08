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

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Username)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.FullName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.PasswordHash)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.Salt)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Role)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("User");

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

                // Unique constraints
                entity.HasIndex(e => e.Username)
                    .IsUnique()
                    .HasDatabaseName("IX_User_Username");

                entity.HasIndex(e => e.Email)
                    .IsUnique()
                    .HasDatabaseName("IX_User_Email");

                entity.ToTable("Users");
            });

            // Seed initial admin accounts
            SeedInitialUsers(modelBuilder);
        }

        private void SeedInitialUsers(ModelBuilder modelBuilder)
        {
            // Note: In production, you'd want to generate these securely
            var adminSalt = "Q2tL8K9mN5pR7sT1vW3xZ6cF4hJ2kM8q";
            var doctorSalt = "A1bC3dE5fG7hI9jK2lM4nO6pQ8rS0tU";
            var nurseSalt = "X1yZ3aB5cD7eF9gH2iJ4kL6mN8oP0qR";

            // These passwords should be changed immediately after first login
            var adminHash = HashPasswordForSeed("Admin123!", adminSalt);
            var doctorHash = HashPasswordForSeed("Doctor123!", doctorSalt);
            var nurseHash = HashPasswordForSeed("Nurse123!", nurseSalt);

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    FullName = "System Administrator",
                    Email = "admin@mentalhealth.clinic",
                    PasswordHash = adminHash,
                    Salt = adminSalt,
                    Role = "Admin",
                    IsActive = true,
                    CreatedBy = "System",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = 2,
                    Username = "dr.smith",
                    FullName = "Dr. John Smith",
                    Email = "dr.smith@mentalhealth.clinic",
                    PasswordHash = doctorHash,
                    Salt = doctorSalt,
                    Role = "Doctor",
                    IsActive = true,
                    CreatedBy = "System",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = 3,
                    Username = "nurse.jane",
                    FullName = "Jane Doe, RN",
                    Email = "nurse.jane@mentalhealth.clinic",
                    PasswordHash = nurseHash,
                    Salt = nurseSalt,
                    Role = "Nurse",
                    IsActive = true,
                    CreatedBy = "System",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }

        private string HashPasswordForSeed(string password, string salt)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var saltedPassword = password + salt;
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(saltedPassword));
            return Convert.ToBase64String(hashBytes);
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
                        break;

                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = DateTime.UtcNow;
                        break;
                }
            }
        }
    }
}