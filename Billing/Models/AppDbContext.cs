using Microsoft.EntityFrameworkCore;

namespace Billing.Models
{
    public class AppDbContext : DbContext
    {
        protected readonly IConfiguration Configuration;

        public DbSet<AccountEntity> Accounts { get; set; } = null!;

        public AppDbContext(IConfiguration configuration)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            Configuration = configuration;
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(Configuration.GetConnectionString("BillingDataBase"));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccountEntity>().ToTable("AccountEntities");

            modelBuilder.Entity<AccountEntity>(e =>
            {
                e.Property(p => p.Id).ValueGeneratedOnAdd();
                e.Property(p => p.AccountId).UseIdentityAlwaysColumn();
            });
        }
    }
}
