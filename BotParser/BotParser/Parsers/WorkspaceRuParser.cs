using BotParser.Services;
using HtmlAgilityPack;
using PuppeteerSharp;
using System.Web;

namespace BotParser.Parsers
{
    public class WorkspaceRuParser
    {
        private readonly Random _rnd = new();
        private readonly IProxyProvider _proxy;

        public WorkspaceRuParser(IProxyProvider proxy) { _proxy = proxy; }

        public record WsOrder(
            long TenderId,
            string Title,
            string Url,
            string Budget,
            string Deadline,
            string Published,
            DateTime ParsedAt);

        public async Task<List<WsOrder>> GetActiveTendersAsync(int? categorySlug = 1)
        {
            var orders = new List<WsOrder>();
            var slug = categorySlug.HasValue ? FreelanceService.CategoryIdToSlug.GetValueOrDefault(categorySlug.Value) : null;

            await new BrowserFetcher().DownloadAsync();
            var args = new List<string>
        {
            "--no-sandbox", "--disable-setuid-sandbox",
            "--disable-dev-shm-usage", "--disable-gpu",
            "--no-zygote", "--single-process"
        };

            if (_proxy.IsEnabled)
            {
                args.Add($"--proxy-server={_proxy.Host}:{_proxy.Port}");
            }

            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = args.ToArray()
            });

            await using var page = await browser.NewPageAsync();
            await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            if (_proxy.IsEnabled)
            {
                await page.AuthenticateAsync(new Credentials
                {
                    Username = _proxy.Username,
                    Password = _proxy.Password
                });
            }

            string url = slug == null
                ? "https://workspace.ru/tenders/?SORT=public&ORDER=0"
                : $"https://workspace.ru/tenders/{slug}/";

            await page.GoToAsync(url, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });
            await Task.Delay(_rnd.Next(5000, 8000));

            // Прокрутка — подгружаем все тендеры
            for (int i = 0; i < 4; i++)
            {
                await page.EvaluateExpressionAsync("window.scrollBy(0, 2000)");
                await Task.Delay(2500);
            }

            var html = await page.GetContentAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var cards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'vacancies__card') and contains(@class, '_tender')]");
            if (cards == null) return orders;

            foreach (var card in cards.Take(10))
            {
                try
                {
                    // КЛЮЧЕВАЯ ПРОВЕРКА: только "Идет прием заявок"
                    var statusNode = card.SelectSingleNode(".//div[contains(@class, 'tendercart-type__status-public') or contains(text(), 'Идет прием заявок')]");
                    if (statusNode == null || !statusNode.InnerText.Contains("Идет прием заявок"))
                        continue;

                    var linkNode = card.SelectSingleNode(".//a[contains(@href, '/tenders/')]");
                    if (linkNode == null) continue;

                    var href = linkNode.GetAttributeValue("href", "");
                    if (string.IsNullOrEmpty(href)) continue;

                    // ID из URL: ...-17986/
                    var idMatch = System.Text.RegularExpressions.Regex.Match(href, @"-(\d+)/?$");
                    if (!idMatch.Success) continue;

                    var tenderId = long.Parse(idMatch.Groups[1].Value);
                    var fullUrl = "https://workspace.ru" + href.TrimEnd('/');

                    var title = HttpUtility.HtmlDecode(linkNode.InnerText.Trim());

                    // Бюджет
                    var budgetNode = card.SelectSingleNode(".//div[contains(@class, 'b-tender__info-item-text')]//span[contains(@class, 'rub')]/preceding-sibling::text()");
                    var budget = budgetNode != null
                        ? "до " + HttpUtility.HtmlDecode(budgetNode.GetDirectInnerText().Trim())
                        : "Не указан";

                    // Дедлайн
                    var deadlineNode = card.SelectSingleNode(".//div[text()='Крайний срок приема заявок:']/following-sibling::div");
                    var deadline = deadlineNode != null
                        ? HttpUtility.HtmlDecode(deadlineNode.InnerText.Trim())
                        : "Не указан";

                    // Дата публикации
                    var publishedNode = card.SelectSingleNode(".//div[text()='Опубликован']/following-sibling::div");
                    var published = publishedNode != null
                        ? HttpUtility.HtmlDecode(publishedNode.InnerText.Trim())
                        : "Недавно";

                    orders.Add(new WsOrder(
                        TenderId: tenderId,
                        Title: title,
                        Url: fullUrl,
                        Budget: budget,
                        Deadline: deadline,
                        Published: published,
                        ParsedAt: DateTime.UtcNow
                    ));
                }
                catch { continue; }
            }

            return orders;
        }
    }
}
