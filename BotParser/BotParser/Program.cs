using BotParser.Db;
using BotParser.Parsers;
using BotParser.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Telegram.Bot;


namespace BotParser
{
    public class Program
    {
        static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var logPath = Path.Combine(AppContext.BaseDirectory, "logs");
            if (!Directory.Exists(logPath))
                Directory.CreateDirectory(logPath);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File(
                    path: "logs/bot-.log",
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 10_485_760, // 10 МБ
                    retainedFileCountLimit: 31,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            builder.Host.UseSerilog();

            // Telegram Bot
            builder.Services.AddHostedService<BotService>();
            builder.Services.AddHttpClient("kwork");
            builder.Services.AddHttpClient("youdo", client =>
            {
                client.BaseAddress = new Uri("https://youdo.com/");
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                client.DefaultRequestHeaders.Add("Origin", "https://youdo.com");
                client.DefaultRequestHeaders.Add("Referer", "https://youdo.com/tasks");
            });
            builder.Services.AddSingleton<ITelegramBotClient>(sp =>
                new TelegramBotClient("8521111908:AAHaiDcpn54kOXpt0EexRpw7sf10MPXv"));
            builder.Services.AddSingleton(new MobileProxyService(
    changeIpUrl: "https://changeip.mobileproxy.space/?proxy_key=b8a11e393b4321eba7f497f208c2fdbb&format=json",
    checkIpUrl: "https://mobileproxy.space/api.html?command=proxy_ip&proxy_id=438773",
    bearerToken: "a0da7f8302087053ba2d36847b2780d8"
));

            // База SQLite
            builder.Services.AddDbContext<KworkBotDbContext>(options =>
                options.UseSqlite("Data Source=kworkbot17.db"));

            // Сервисы
            builder.Services.AddScoped<FreelanceService>();
            builder.Services.AddScoped<KworkParser>();
            builder.Services.AddHostedService<CategoryCheckerService>();
            builder.Services.AddScoped<FlParser>();
            builder.Services.AddScoped<YoudoParser>();
            builder.Services.AddScoped<FreelanceRuParser>();
            builder.Services.AddScoped<WorkspaceRuParser>();
            builder.Services.AddScoped<ProfiRuParser>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<KworkBotDbContext>();
                //db.Database.EnsureDeleted();   // удаляем старый кривой файл (один раз)
                db.Database.EnsureCreated();
            }

            try
            {
                Log.Information("Бот запускается...");
                app.Run();

            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Бот упал при старте");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
