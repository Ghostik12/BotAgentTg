using BotParser.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BotParser.Services
{
    public class AutoCheckerService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        public AutoCheckerService(IServiceProvider sp) => _sp = sp;

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), ct);

                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<KworkBotDbContext>();
                var kwork = scope.ServiceProvider.GetRequiredService<KworkService>();

                var users = await db.Users
                    .Where(u => u.NotificationInterval != "off" && u.SelectedCategories.Any())
                    .ToListAsync(ct);

                foreach (var user in users)
                {
                    if (user.NotificationInterval == "instant" ||
                        user.NotificationInterval == "15min" ||
                        user.NotificationInterval == "hour" ||
                        user.NotificationInterval == "day")
                    {
                        await kwork.CheckAndSendNewOrders(user.Id);
                    }
                }
            }
        }
    }
}
