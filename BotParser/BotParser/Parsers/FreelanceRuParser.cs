using HtmlAgilityPack;
using PuppeteerSharp;
using System.Web;

namespace BotParser.Parsers
{
    public class FreelanceRuParser
    {
        private readonly Random _rnd = new();

        public record FrOrder(
            long ProjectId,
            string Title,
            string Url,
            string? Budget,
            string? Description,
            string? Deadline,
            string? Category,
            DateTime PublishedAt);

        public async Task<List<FrOrder>> GetNewOrdersAsync(int? categoryId = null)
        {
            var orders = new List<FrOrder>();

            await new BrowserFetcher().DownloadAsync();
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[]
    {
        "--no-sandbox",
        "--disable-setuid-sandbox",
        "--disable-dev-shm-usage",
        "--disable-gpu",
        "--no-zygote",
        "--single-process",
        "--proxy-server=http://gate.mobileproxy.space:10000"
    }
                // НЕ ПИШИ ExecutablePath НИГДЕ — УДАЛИ СТРОЧКУ!
            });

            await using var page = await browser.NewPageAsync();
            await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            await page.SetViewportAsync(new ViewPortOptions { Width = 1920, Height = 1080 });

            string url = categoryId.HasValue && categoryId.Value != 0
                ? $"https://freelance.ru/project/search?c%5B%5D={categoryId.Value}"
                : "https://freelance.ru/project/search";

            await page.GoToAsync(url, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });
            await Task.Delay(_rnd.Next(5000, 9000));

            // Прокрутка для подгрузки
            await page.EvaluateExpressionAsync("() => window.scrollTo(0, document.body.scrollHeight)");
            await Task.Delay(3000);

            var html = await page.GetContentAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var cards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'project-item-default-card')]");
            if (cards == null) return orders;

            foreach (var card in cards.Take(10))
            {
                try
                {
                    var link = card.SelectSingleNode(".//h2//a | .//a[contains(@href, '/projects/')]");
                    if (link == null) continue;

                    var href = link.GetAttributeValue("href", "");
                    if (!href.Contains("/projects/")) continue;

                    var idMatch = System.Text.RegularExpressions.Regex.Match(href, @"\/(\d+)\.html");
                    if (!idMatch.Success)
                    {
                        // Fallback — ищем любые цифры в конце
                        idMatch = System.Text.RegularExpressions.Regex.Match(href, @"-(\d+)\.html$");
                    }
                    if (!idMatch.Success) continue;

                    var projectId = long.Parse(idMatch.Groups[1].Value);
                    var fullUrl = "https://freelance.ru" + href;
                    var title = HttpUtility.HtmlDecode(link.InnerText.Trim());

                    var budgetNode = card.SelectSingleNode(".//div[contains(@class, 'cost')]");
                    var budget = budgetNode != null ? HttpUtility.HtmlDecode(budgetNode.InnerText.Trim()) : "Договорная";

                    var descNode = card.SelectSingleNode(".//a[contains(@class, 'description')]");
                    var description = descNode != null ? HttpUtility.HtmlDecode(descNode.InnerText.Trim()) : null;

                    var deadlineNode = card.SelectSingleNode(".//div[contains(@class, 'term')]//span");
                    var deadline = deadlineNode != null ? HttpUtility.HtmlDecode(deadlineNode.InnerText.Trim()) : null;

                    var catNode = card.SelectSingleNode(".//div[contains(@class, 'specs-list')]//b");
                    var category = catNode != null ? HttpUtility.HtmlDecode(catNode.InnerText.Trim()) : "Без категории";

                    orders.Add(new FrOrder(
                        ProjectId: projectId,
                        Title: title,
                        Url: fullUrl,
                        Budget: budget,
                        Description: description,
                        Deadline: deadline,
                        Category: category,
                        PublishedAt: DateTime.UtcNow
                    ));
                }
                catch { continue; }
            }

            return orders;
        }
    }
}
