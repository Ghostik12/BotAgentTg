using BotParser.Db;
using BotParser.Parsers;
using BotParser.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;


namespace BotParser
{
    public class Program
    {
        static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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
                new TelegramBotClient("8565915816:AAFeCJoTB0nwKyLD0z_ruoggkUBrWOvxplY"));

            // База SQLite
            builder.Services.AddDbContext<KworkBotDbContext>(options =>
                options.UseSqlite("Data Source=kworkbot3.db"));

            // Сервисы
            builder.Services.AddScoped<FreelanceService>();
            builder.Services.AddScoped<KworkParser>();
            builder.Services.AddHostedService<CategoryCheckerService>();
            builder.Services.AddScoped<FlParser>();
            builder.Services.AddScoped<YoudoParser>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<KworkBotDbContext>();
                //db.Database.EnsureDeleted();   // удаляем старый кривой файл (один раз)
                db.Database.EnsureCreated();
            }

            app.Run();
        }
    }
}
