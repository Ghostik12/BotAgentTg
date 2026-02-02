using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VkBotParser.Db;
using VkBotParser.Models;

namespace VkBotParser
{
    class Program
    {
        private const ulong GroupId = 235726596; // положительный ID группы, UL — чтобы ulong

        private static readonly HttpClient HttpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // База данных
                    services.AddDbContext<KworkBotDbContext>(options =>
                        options.UseSqlite("Data Source=bot.db"));

                    // Логирование (консоль + можно добавить файл/Serilog)
                    services.AddLogging(logging =>
                    {
                        logging.AddConsole();
                        // logging.AddDebug();
                    });

                    // Привязываем секцию "Vk" из конфига
                    services.Configure<VkSettings>(hostContext.Configuration.GetSection("Vk"));

                    // Регистрируем бота
                    services.AddSingleton<VkBot>();
                })
                .Build();

            var config = host.Services.GetRequiredService<IConfiguration>();

            string accessToken = config["Vk:AccessToken"]
                ?? Environment.GetEnvironmentVariable("VK_TOKEN")
                ?? throw new InvalidOperationException("Токен не найден");

            // Получаем сервисы
            var bot = host.Services.GetRequiredService<VkBot>();

            // Токен лучше не хардкодить, но для теста можно так
            //string accessToken = "vk1.a.твой_токен_группы_здесь";

            try
            {
                Console.WriteLine("Запуск VK бота...");
                await bot.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Критическая ошибка запуска бота:");
                Console.WriteLine(ex);
            }

            // Чтобы приложение не завершилось сразу после ошибки
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }
}