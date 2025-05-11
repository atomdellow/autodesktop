using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Diagnostics; // Added for Debug.WriteLine

namespace AutoDesktopApplication.Models
{
    /// <summary>
    /// Entity Framework Core database context for the application
    /// </summary>
    public class AppDbContext : DbContext
    {
        public DbSet<Project> Projects { get; set; }
        public DbSet<Workflow> Workflows { get; set; }
        public DbSet<TaskBot> TaskBots { get; set; }
        public DbSet<WorkflowRun> WorkflowRuns { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Project entity
            modelBuilder.Entity<Project>()
                .HasMany(p => p.Workflows)
                .WithOne(w => w.Project)
                .HasForeignKey(w => w.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Workflow entity
            modelBuilder.Entity<Workflow>()
                .HasMany(w => w.TaskBots)
                .WithOne(t => t.Workflow)
                .HasForeignKey(t => t.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Configure WorkflowRun entity
            modelBuilder.Entity<Workflow>()
                .HasMany(w => w.WorkflowRuns)
                .WithOne(r => r.Workflow)
                .HasForeignKey(r => r.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure TaskBot entity
            modelBuilder.Entity<TaskBot>()
                .Property(t => t.InputData)
                .IsRequired(false);

            modelBuilder.Entity<TaskBot>()
                .Property(t => t.AiDecisionCriteria)
                .IsRequired(false);

            modelBuilder.Entity<TaskBot>()
                .Property(t => t.ScreenshotData)
                .IsRequired(false);
        }

        // Method to get database path in user's AppData folder
        public static string GetDatabasePath()
        {
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string appDataPath = Path.Combine(localAppDataPath, "AutoDesktopApplication");

            if (!Directory.Exists(appDataPath))
            {
                try
                {
                    Directory.CreateDirectory(appDataPath);
                    if (!Directory.Exists(appDataPath))
                    {
                        throw new Exception($"CRITICAL_ERROR: Failed to create directory at {appDataPath}. Please check permissions or security software interference.");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"CRITICAL_ERROR: Exception during directory creation at {appDataPath}. Original exception: {ex.Message}", ex);
                }
            }
            
            string dbFilePath = Path.Combine(appDataPath, "autodesktop.db");
            return dbFilePath;
        }
    }
}