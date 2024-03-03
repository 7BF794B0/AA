namespace JwtAuth.Models
{
    public static class SeedData
    {
        public static void Initialize(IConfiguration configuration)
        {
            using (var context = new AppDbContext(configuration))
            {
                if (context.Users.Any())
                {
                    return;
                }

                context.SaveChanges();
            }
        }
    }
}
