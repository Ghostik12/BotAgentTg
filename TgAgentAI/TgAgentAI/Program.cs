using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TgAgentAI.Infrastructure;
using TgAgentAI.Jobs;
using TgAgentAI.Services;

namespace TgAgentAI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddAppServices(builder.Configuration);
            builder.Services.AddHostedService<TelegramBotHostedService>();
            builder.Services.AddScoped<ContentPlanJob>();

            var app = builder.Build();

            app.UseHangfireDashboard();
            RecurringJob.AddOrUpdate<ContentPlanJob>(
                "weekly-plan",
                job => job.GenerateWeeklyPlan(),
                "0 9 * * 1"); // Каждое утро понедельника

            app.Run();
        }
    }
}
