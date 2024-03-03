using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.NameTranslation;

namespace TaskTracker.Models
{
    public class AppDbContext : DbContext
    {
        protected readonly IConfiguration Configuration;

        public DbSet<TaskEnity> Tasks { get; set; } = null!;

        public AppDbContext(IConfiguration configuration)
        {
            Configuration = configuration;
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(Configuration.GetConnectionString("TaskTrackerDataBase"));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TaskEnity>().ToTable("Tasks");
            modelBuilder.HasPostgresEnum<StatusEnum>(nameTranslator: new NpgsqlNullNameTranslator());

            modelBuilder.Entity<TaskEnity>(e =>
            {
                e.Property(p => p.Id).UseIdentityAlwaysColumn();
                e.Property(p => p.Status).HasConversion(new EnumToStringConverter<StatusEnum>());
            });
        }
    }
}
