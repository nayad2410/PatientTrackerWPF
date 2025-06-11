using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PatientTrackerWPF.MainWindow;

namespace PatientTrackerWPF.Data
{
   public class AppDbContext : DbContext
   
    {
        public DbSet<ScoreEntry> ScoreEntries { get; set; }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ScoreEntry>().HasKey(s => s.Id);
       
         /*   modelBuilder.Entity<ScoreEntry>().Property(s => s.Score).IsRequired();
            modelBuilder.Entity<ScoreEntry>().Property(s => s.Date).IsRequired();*/
        }

        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    modelBuilder.Entity<Patient>().HasData(new Patient
        //    {
        //        Id = Guid.NewGuid(),
        //        PatientIdentifier = "TMS-001",
        //        DateOfBirth = new DateTime(1990, 5, 1),
        //        CreatedBy = "System"
        //    });
        //}


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Load the config from appsettings.json
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var connectionString = config.GetConnectionString("PatientDb");
            optionsBuilder.UseSqlServer(connectionString);
        }
    }
}
