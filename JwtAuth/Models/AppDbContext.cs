using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.NameTranslation;
using Contracts;

namespace JwtAuth.Models
{
    public class AppDbContext : DbContext
    {
        protected readonly IConfiguration Configuration;

        public DbSet<UserEnity> Users { get; set; } = null!;

        public AppDbContext(IConfiguration configuration)
        {
            Configuration = configuration;
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(Configuration.GetConnectionString("AuthDataBase"));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEnity>().ToTable("Users");
            modelBuilder.HasPostgresEnum<RoleEnum>(nameTranslator: new NpgsqlNullNameTranslator());

            modelBuilder.Entity<UserEnity>(e =>
            {
                e.Property(p => p.Id).ValueGeneratedOnAdd();
                e.Property(p => p.PublicId).UseIdentityAlwaysColumn();
                e.Property(p => p.Role).HasConversion(new EnumToStringConverter<RoleEnum>());
                // BREDIK: https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding#limitations-of-model-seed-data
                e.HasData(
                    new UserEnity { Id = Guid.NewGuid(), PublicId = 1, Email = "Bronzebeard@popuginc.com", Password = "qwerty", Name = "Popug Evgenevich", Role = RoleEnum.Popug },
                    new UserEnity { Id = Guid.NewGuid(), PublicId = 2, Email = "Jorg@popuginc.com", Password = "qwerty123", Name = "Popug Sergeevich", Role = RoleEnum.Popug },
                    new UserEnity { Id = Guid.NewGuid(), PublicId = 3, Email = "Loken@popuginc.com", Password = "Qwerty123!", Name = "Popug Dmitrievich", Role = RoleEnum.Popug },
                    new UserEnity { Id = Guid.NewGuid(), PublicId = 4, Email = "Falstad@popuginc.com", Password = "12345678", Name = "Popug Viktorovich", Role = RoleEnum.Admin },
                    new UserEnity { Id = Guid.NewGuid(), PublicId = 5, Email = "Thaurissan@popuginc.com", Password = "password", Name = "Popug Alekseevich", Role = RoleEnum.Accountant }
                );
            });
        }
    }
}
