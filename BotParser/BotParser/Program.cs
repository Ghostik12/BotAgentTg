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
            builder.Services.AddSingleton<ITelegramBotClient>(sp =>
                new TelegramBotClient("7972227901:AAH_tAaKBEmnTHTrvRE1Kh5Sf4S5lqE"));

            // База SQLite
            builder.Services.AddDbContext<KworkBotDbContext>(options =>
                options.UseSqlite("Data Source=kworkbot2.db"));

            // Сервисы
            builder.Services.AddScoped<FreelanceService>();
            builder.Services.AddScoped<KworkParser>();
            builder.Services.AddHostedService<CategoryCheckerService>();
            builder.Services.AddScoped<FlParser>();

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
