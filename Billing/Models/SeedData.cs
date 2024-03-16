namespace Billing.Models
{
    public static class SeedData
    {
        public static void Initialize(IConfiguration configuration)
        {
            using (var context = new AppDbContext(configuration))
            {
                if (context.Accounts.Any())
                {
                    return;
                }

                context.SaveChanges();
            }
        }
    }
}
