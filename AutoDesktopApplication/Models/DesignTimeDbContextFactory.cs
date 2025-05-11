using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AutoDesktopApplication.Models // Ensure this namespace matches your AppDbContext's namespace
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            // Use the same path as the runtime application
            string dbPath = AppDbContext.GetDatabasePath(); 
            // Console.WriteLine($"DesignTimeDbContextFactory: Using DB path: {dbPath}");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
