using BotParser.Db;
using BotParser.Models;
using BotParser.Parsers;
using BotParser.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                new TelegramBotClient("8537792489:AAGZXlowJn2UTzAIZ2hwJxQahyG52aUU"));
            builder.Services.Configure<MobileProxyConfig>(
    builder.Configuration.GetSection("MobileProxy"));

            builder.Services.AddSingleton<MobileProxyService>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                return new MobileProxyService(
                    config["MobileProxy:ChangeIpUrl"]!,
                    config["MobileProxy:CheckIpUrl"]!,
                    config["MobileProxy:BearerToken"]!
                );
            });
            builder.Services.AddScoped<IProxyProvider, MobileProxyProvider>();

            builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\bot\keys")) // или C:\bot\keys на Windows /opt/botparser/keys
    .SetApplicationName("BotParser");

            // База SQLite
            builder.Services.AddDbContext<KworkBotDbContext>(options =>
                options.UseSqlite("Data Source=kworkbot18.db"));

            // Сервисы
            builder.Services.AddScoped<FreelanceService>();
            builder.Services.AddScoped<KworkParser>();
            builder.Services.AddHostedService<CategoryCheckerService>();
            builder.Services.AddScoped<FlParser>();
            builder.Services.AddScoped<YoudoParser>();
            builder.Services.AddScoped<FreelanceRuParser>();
            builder.Services.AddScoped<WorkspaceRuParser>();
            builder.Services.AddScoped<ProfiRuParser>();
            builder.Services.AddScoped<EncryptionService>();
            builder.Services.AddScoped<IEncryptionService, EncryptionService>();
            builder.Services.AddScoped<Func<long, ProfiRuParser>>(sp =>
            telegramId => new ProfiRuParser(sp.GetRequiredService<KworkBotDbContext>(), telegramId, sp.GetRequiredService<IEncryptionService>(), sp.GetRequiredService<IProxyProvider>()));

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
