using HtmlAgilityPack;
using PuppeteerSharp;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace BotParser.Parsers
{
    public class KworkParser
    {
        private readonly Random _rnd = new();

        public record KworkOrder(
            string Title,
            string Url,
            string? DesiredBudget,
            string? AllowedBudget,
            string? Description,
            long ProjectId,
            bool AlreadyViewed,
            DateTime ParsedAt);

        public async Task<List<KworkOrder>> GetNewOrdersAsync(int? categoryId = null)
        {
            var orders = new List<KworkOrder>();

            // Запускаем headless Chrome
            await new BrowserFetcher().DownloadAsync();
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            using var page = await browser.NewPageAsync();

            // Настраиваем браузер как реальный юзер
            await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            await page.SetViewportAsync(new ViewPortOptions { Width = 1920, Height = 1080 });

            var url = categoryId.HasValue && categoryId.Value != 0
                ? $"https://kwork.ru/projects?c={categoryId.Value}"
                : "https://kwork.ru/projects";

            await page.GoToAsync(url, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } }); // Ждём полной загрузки JS

            await Task.Delay(_rnd.Next(3000, 5000)); // Доп. задержка для динамики

            // Получаем готовый HTML после JS
            var html = await page.GetContentAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Точный селектор для карточек (из твоего HTML)
            var cards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'want-card--list')]");
            if (cards == null) return orders;

            foreach (var card in cards.Take(15))
            {
                try
                {
                    // Заголовок (теперь точно не null, потому что JS загрузил)
                    var titleNode = card.SelectSingleNode(".//h1[contains(@class, 'wants-card__header-title')]//a");
                    if (titleNode == null) continue;

                    var title = HttpUtility.HtmlDecode(titleNode.InnerText.Trim());
                    var relativeUrl = titleNode.GetAttributeValue("href", "");
                    if (!relativeUrl.StartsWith("/projects/")) continue;

                    var fullUrl = "https://kwork.ru" + relativeUrl;

                    long projectId = 0;
                    var match = System.Text.RegularExpressions.Regex.Match(relativeUrl, @"/projects/(\d+)");
                    if (match.Success)
                    {
                        long.TryParse(match.Groups[1].Value, out projectId);
                    }

                    // Просмотрено? (проверяем стиль и текст)
                    var viewedBlock = card.SelectSingleNode(".//div[contains(@class, 'want-card__mark-viewed')]");
                    var alreadyViewed = viewedBlock != null &&
                                        !viewedBlock.GetAttributeValue("style", "").Contains("display: none") &&
                                        viewedBlock.InnerText.Contains("ПРОСМОТРЕНО");

                    if (alreadyViewed) continue;

                    // Описание (только видимая часть)
                    var descNode = card.SelectSingleNode(".//div[contains(@class, 'wants-card__description-text')]//div[contains(@class, 'overflow-hidden')]//div[contains(@class, 'breakwords')]");
                    var description = descNode != null
                        ? HttpUtility.HtmlDecode(descNode.InnerText.Trim()).Replace("Задача:", "").Replace("\n", " ").Trim()
                        : null;

                    // Желаемый бюджет
                    var desiredNode = card.SelectSingleNode(".//div[contains(@class, 'wants-card__price')]//div[contains(@class, 'd-inline')]");
                    var desiredBudget = desiredNode?.InnerText.Trim() + " ₽";

                    // Допустимый бюджет
                    var allowedNode = card.SelectSingleNode(".//div[contains(@class, 'wants-card__description-higher-price')]//div[contains(@class, 'd-inline')]");
                    var allowedBudget = allowedNode != null ? "до " + allowedNode.InnerText.Trim() + " ₽" : null;

                    orders.Add(new KworkOrder(
                        Title: title,
                        Url: fullUrl,
                        DesiredBudget: desiredBudget,
                        AllowedBudget: allowedBudget,
                        Description: description,
                        ProjectId: projectId,
                        AlreadyViewed: alreadyViewed,
                        ParsedAt: DateTime.UtcNow
                    ));
                }
                catch { continue; }
            }

            return orders;
        }
    }
}
