using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParserFlightTickets.Config;
using ParserFlightTickets.Services.Api;
using ParserFlightTickets.Services.Data;
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

                // Минимальный конфиг (только токен и канал)
                var botConfig = new BotConfig
                {
                    Telegram = configuration.GetSection("Bot").Get<TelegramConfig>() ?? new TelegramConfig(),
                    TravelPayouts = configuration.GetSection("TravelPayouts").Get<TravelPayoutsConfig>() ?? new TravelPayoutsConfig(),
                };
                services.AddSingleton(botConfig);

                services.AddSingleton<TravelPayoutsService>();
                services.AddSingleton<TelegramPublisher>();
                services.AddSingleton<OstrovokService>();
                services.AddSingleton<LevelTravelService>();
                services.AddHostedService<TelegramPublisher>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite("Data Source=bot5.db"),
                    contextLifetime: ServiceLifetime.Scoped);
                services.AddScoped<SettingsService>();
                //services.AddSingleton<ISchedulerFactory>(provider => provider.GetRequiredService<ISchedulerFactory>());

                // Quartz — регистрируем, но интервал зададим позже
                services.AddQuartz(q =>
                {
                    //q.UseMicrosoftDependencyInjectionJobFactory();
                    //var jobKey = new JobKey("DealSearchJob");
                    //q.AddJob<DealSearchJob>(opts => opts.WithIdentity(jobKey));
                    //q.AddTrigger(opts => opts
                    //    .ForJob(jobKey)
                    //    .WithIdentity("DealSearchTrigger")
                    //    .StartNow()
                    //    .WithSimpleSchedule(x => x
                    //        .WithIntervalInMinutes(60)  // временный дефолт
                    //        .RepeatForever()));

                    //// Hotel
                    //var hotelJobKey = new JobKey("HotelSearchJob");
                    //q.AddJob<HotelSearchJob>(opts => opts.WithIdentity(hotelJobKey));
                    //q.AddTrigger(opts => opts
                    //    .ForJob(hotelJobKey)
                    //    .WithIdentity("HotelTrigger")
                    //    .StartNow()
                    //    .WithSimpleSchedule(x => x
                    //        .WithIntervalInMinutes(120)  // например каждые 2 часа
                    //        .RepeatForever()));

                    var tourJobKey = new JobKey("TourSearchJob");
                    q.AddJob<TourSearchJob>(opts => opts.WithIdentity(tourJobKey));
                    q.AddTrigger(opts => opts
                        .ForJob(tourJobKey)
                        .WithIdentity("TourTrigger")
                        .StartNow()
                        .WithSimpleSchedule(x => x
                            .WithIntervalInMinutes(180)
                            .RepeatForever()));
                });

                services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
            })
            .Build();

        // После создания host получаем сервисы
        var settingsService = host.Services.GetRequiredService<SettingsService>();
        var botConfig = host.Services.GetRequiredService<BotConfig>();

        // Загружаем актуальный интервал из БД
        int checkIntervalMinutesFl = settingsService.GetInt("Flights_CheckIntervalMinutes");
        int checkIntervalMinutesHo = settingsService.GetInt("Hotels_CheckIntervalMinutes");
        int checkIntervalMinutesTo = settingsService.GetInt("Tours_CheckIntervalMinutes");

        // Переопределяем триггер Quartz с актуальным интервалом
        var scheduler = await host.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
        var triggerKey = new TriggerKey("DealSearchTrigger");
        var newTrigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob("DealSearchJob")
            .StartNow()
            .WithSimpleSchedule(x => x
                .WithIntervalInMinutes(checkIntervalMinutesFl)
                .RepeatForever())
            .Build();

        //var triggerKeyHo = new TriggerKey("HotelTrigger");
        //var newTriggerHo = TriggerBuilder.Create()
        //    .WithIdentity(triggerKeyHo)
        //    .ForJob("HotelSearchJob")
        //    .StartNow()
        //    .WithSimpleSchedule(x => x
        //        .WithIntervalInMinutes(checkIntervalMinutesHo)
        //        .RepeatForever())
        //    .Build();

        var triggerKeyTo = new TriggerKey("TourTrigger");
        var newTriggerTo = TriggerBuilder.Create()
            .WithIdentity(triggerKeyTo)
            .ForJob("TourSearchJob")
            .StartNow()
            .WithSimpleSchedule(x => x
                .WithIntervalInMinutes(checkIntervalMinutesTo)
                .RepeatForever())
            .Build();

        //await scheduler.RescheduleJob(triggerKey, newTrigger);
        //await scheduler.RescheduleJob(triggerKeyHo, newTriggerHo);
        await scheduler.RescheduleJob(triggerKeyTo, newTriggerTo);

        Console.WriteLine($"Канал для публикаций: {botConfig.Telegram.ChannelId}");
        Console.WriteLine($"Интервал проверки: {checkIntervalMinutesFl} минут (из БД)");
        Console.WriteLine("Бот стартовал.");

        await host.RunAsync();
    }
}