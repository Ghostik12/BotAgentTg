using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notion.Client;
using Telegram.Bot;

namespace TgAgentAI.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration config)
        {
            // Telegram
            services.AddSingleton<ITelegramBotClient>(sp =>
                new TelegramBotClient(config["Telegram:BotToken"]!));

            // Gemini
            services.AddSingleton<GenerativeAI>(sp =>
                new GenerativeAI(config["Gemini:ApiKey"]!));

            // Google Sheets
            services.AddSingleton<SheetsService>(sp =>
            {
                var credential = GoogleCredential
                    .FromFile(config["GoogleSheets:CredentialsJson"])
                    .CreateScoped(SheetsService.Scope.Spreadsheets);
                return new SheetsService(new() { HttpClientInitializer = credential });
            });

            // Notion
            services.AddSingleton<INotionClient>(sp =>
                NotionClientFactory.Create(new ClientOptions { AuthToken = config["Notion:Token"] }));

            // Hangfire (in-memory for demo)
            services.AddHangfire(x => x.UseInMemoryStorage());
            services.AddHangfireServer();

            return services;
        }
    }
}
