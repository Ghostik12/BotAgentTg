using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using PuppeteerSharp;
using System.Text.RegularExpressions;

namespace BotParser.Parsers
{
    public class ProfiRuParser
    {
        private readonly Random _rnd = new();
        private readonly string _login;
        private readonly string _password;

        public record ProfiOrder(
            long OrderId,
            string Title,
            string Budget,
            string Description,
            string City,
            string Published,
            string Url);

        public ProfiRuParser(IConfiguration config)
        {
            _login = config["Profi:Login"] ?? throw new ArgumentNullException("Profi:Login");
            _password = config["Profi:Password"] ?? throw new ArgumentNullException("Profi:Password");
        }

        public async Task<List<ProfiOrder>> GetOrdersAsync(string query)
        {
            var orders = new List<ProfiOrder>();

            await new BrowserFetcher().DownloadAsync();
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            await using var page = await browser.NewPageAsync();

            await page.SetViewportAsync(new ViewPortOptions { Width = 1920, Height = 1080 });
            await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            // Логин через POST (как в Python-скрапере — надёжно)
            await page.GoToAsync("https://profi.ru/backoffice/n.php", new NavigationOptions { Timeout = 60000 });

            // Ждём формы
            await page.WaitForSelectorAsync("input[placeholder='Логин или телефон']", new WaitForSelectorOptions { Timeout = 30000 });
            await page.TypeAsync("input[placeholder='Логин или телефон']", _login);
            await page.TypeAsync("input[placeholder='Пароль']", _password);
            await page.ClickAsync("button[data-testid='enter_with_sms_btn']");

            await Task.Delay(10000);

            // Если SMS — ручной ввод (как в Python — ждём 60 сек)
            if (await page.EvaluateExpressionAsync<bool>("document.body.innerText.includes('код из SMS')"))
            {
                await page.TypeAsync("input[placeholder='Код из SMS']", "000000"); // Замени на реальный код, если нужно
                await page.ClickAsync("button[type='submit']");
                await Task.Delay(5000);
            }

            // Переходим в backoffice
            await page.GoToAsync("https://profi.ru/backoffice/n.php", new NavigationOptions { Timeout = 60000 });
            await Task.Delay(8000);

            // Поиск (как в Python — через поле)
            await page.ClickAsync("button[data-testid='fulltext_view_mode_test_id']");
            await Task.Delay(1000);
            await page.TypeAsync("input[placeholder='Какой заказ ищете?']", query);
            await page.Keyboard.PressAsync("Enter");
            await Task.Delay(10000);

            // Скролл (как в Python)
            for (int i = 0; i < 5; i++)
            {
                await page.EvaluateExpressionAsync("window.scrollBy(0, 2000)");
                await Task.Delay(3000);
            }

            // Парсинг (BeautifulSoup-стиль, но HtmlAgilityPack)
            var html = await page.GetContentAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var cards = doc.DocumentNode.SelectNodes("//a[contains(@href, '/backoffice/order/') or contains(@data-testid, '_order-snippet')]") ?? new HtmlNodeCollection(null);

            foreach (var card in cards.Take(10))
            {
                try
                {
                    var href = card.GetAttributeValue("href", "");
                    var idMatch = Regex.Match(href, @"o=(\d+)");
                    if (!idMatch.Success) continue;

                    var orderId = long.Parse(idMatch.Groups[1].Value);
                    var titleNode = card.SelectSingleNode(".//h3");
                    var title = titleNode?.InnerText.Trim() ?? "Без названия";

                    var budgetNode = card.SelectSingleNode(".//span[contains(text(), '₽')] | .//div[contains(@class, 'price')]");
                    var rawBudget = budgetNode?.InnerText.Trim() ?? "Не указан";
                    var budget = CleanProfiBudget(rawBudget);

                    var descNode = card.SelectSingleNode(".//p[contains(@class, 'description') or contains(@class, 'text')] | .//div[contains(@class, 'order-snippet')]//p");
                    var description = descNode?.InnerText.Trim() ?? "";

                    var cityNode = card.SelectSingleNode(".//span[contains(text(), '·')] | .//div[contains(text(), 'Дистанционно')] | .//span[contains(text(), 'Москва')]");
                    var city = cityNode?.InnerText.Trim() ?? "Россия";

                    var publishedNode = card.SelectSingleNode(".//time | .//span[contains(@class, 'date')] | .//div[contains(text(), 'ноя')]");
                    var published = publishedNode?.InnerText.Trim() ?? "Недавно";

                    var fullUrl = "https://profi.ru" + href;

                    orders.Add(new ProfiOrder(orderId, title, budget, description, city, published, fullUrl));
                }
                catch { continue; }
            }

            return orders;
        }

        private static string CleanProfiBudget(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return "Не указан";

            // Убираем слово false и всё, что после него
            var falseIndex = rawText.IndexOf("false", StringComparison.OrdinalIgnoreCase);
            if (falseIndex >= 0)
                rawText = rawText.Substring(0, falseIndex);

            // Ищем рубли и берём всё до и после них — это и есть бюджет
            var rubIndex = rawText.IndexOf('₽');
            if (rubIndex == -1)
                return "Не указан";

            // Берём ±30 символов вокруг рубля — там точно бюджет
            var start = Math.Max(0, rubIndex - 30);
            var end = Math.Min(rawText.Length, rubIndex + 10);
            var budgetPart = rawText.Substring(start, end - start);

            // Чистим от переносов, табов, лишних пробелов
            var clean = Regex.Replace(budgetPart, @"\s+", " ").Trim();

            // Убираем лишние слова типа "Бюджет:", "от", "до" — оставляем только суть
            clean = Regex.Replace(clean, @"^(Бюджет[:\s]*|от\s+|до\s+)", "", RegexOptions.IgnoreCase);

            // Если остались только цифры и рубль — ок
            if (Regex.IsMatch(clean, @"^\d{1,3}(\s\d{3})*\s*₽$"))
                return clean.Trim();

            // Финальная чистка — оставляем только то, где есть рубль
            var match = Regex.Match(rawText, @"(от|до)?\s*[\d\s]+₽");
            if (match.Success)
                return Regex.Replace(match.Value, @"\s+", " ").Trim();

            return "Не указан";
        }
    }
}
