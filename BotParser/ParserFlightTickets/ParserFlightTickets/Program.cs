using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParserFlightTickets.Config;
using ParserFlightTickets.Services.Api;
using ParserFlightTickets.Services.Scheduler;
using ParserFlightTickets.Services.Telegram;
using Quartz;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((hostContext, services) =>
            {
                var configuration = hostContext.Configuration;

                // Конфиг
                var botConfig = new BotConfig
                {
                    Telegram = configuration.GetSection("Bot").Get<TelegramConfig>() ?? new TelegramConfig(),
                    TravelPayouts = configuration.GetSection("TravelPayouts").Get<TravelPayoutsConfig>() ?? new TravelPayoutsConfig(),
                    SearchSettings = configuration.GetSection("SearchSettings").Get<SearchSettingsConfig>() ?? new SearchSettingsConfig(),
                };
                services.AddSingleton(botConfig);
                services.AddSingleton<TravelPayoutsService>();

                // Telegram сервис как hosted
                services.AddSingleton<TelegramPublisher>();

                services.AddQuartz(q =>
                {
                    q.UseMicrosoftDependencyInjectionJobFactory();

                    var jobKey = new JobKey("DealSearchJob");

                    q.AddJob<DealSearchJob>(opts => opts.WithIdentity(jobKey));

                    q.AddTrigger(opts => opts
                        .ForJob(jobKey)
                        .WithIdentity("DealSearchTrigger")
                        .StartNow()
                        .WithSimpleSchedule(x => x
                            .WithIntervalInMinutes(botConfig.SearchSettings.CheckIntervalMinutes) // из конфига, например 60
                            .RepeatForever()));
                });

                services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
            })
            .Build();
        Console.WriteLine( host.Services.GetRequiredService<BotConfig>().Telegram.ChannelId);
        Console.WriteLine("Бот стартовал. Пока ничего не делает.");
        Console.WriteLine($"Канал для публикаций: {host.Services.GetRequiredService<BotConfig>().Telegram.ChannelId}");

        await host.RunAsync();
    }
}