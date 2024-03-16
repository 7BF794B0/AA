using Quartz;

namespace Billing
{
    public static class BillingCycleService
    {
        public static void AddBillingCycleService(this IServiceCollection services)
        {
            services.AddQuartz(options =>
            {
                options.UseMicrosoftDependencyInjectionJobFactory();

                var jobKey = JobKey.Create(nameof(BillingCycleBackgroundJob));

                options
                .AddJob<BillingCycleBackgroundJob>(JobKey.Create(nameof(BillingCycleBackgroundJob)))
                .AddTrigger(trigger =>
                    trigger.ForJob(jobKey)
                    .WithSchedule(CronScheduleBuilder
                        .DailyAtHourAndMinute(23, 59)
                        .InTimeZone(TimeZoneInfo.Utc)));
            });

            services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });
        }
    }
}
