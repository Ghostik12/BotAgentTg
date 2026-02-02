using VkBotParser.Services;
using HtmlAgilityPack;
using PuppeteerSharp;
using Serilog;
using System.Web;

namespace VkBotParser.Parsers
{
    public class YoudoParser
    {
        private readonly Random _rnd = new();
        private readonly IProxyProvider _proxy;

        public YoudoParser(IProxyProvider proxy)
        {
            _proxy = proxy;
        }

        public record YoudoOrder(
            long TaskId,
            string Title,
            string Url,
            string? Budget,
            string? Description,
            string? Address,
            string? StartDate,
            DateTime ParsedAt);

        public async Task<List<YoudoOrder>> GetNewOrdersAsync(int? categoryId = null)
        {
            var orders = new List<YoudoOrder>();

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

            // Антидетект
            await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            await page.SetViewportAsync(new ViewPortOptions { Width = 1920, Height = 1080 });
            await page.EvaluateExpressionAsync("() => { Object.defineProperty(navigator, 'webdriver', { get: () => false }); }");
            if (_proxy.IsEnabled)
            {
                await page.AuthenticateAsync(new Credentials
                {
                    Username = _proxy.Username,
                    Password = _proxy.Password
                });
            }

            // URL не меняется, но для категории кликнем (если нужно)
            var url = "https://youdo.com/tasks-all-opened-all";
            await page.GoToAsync(url, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded }, // ← Это главное!
                Timeout = 30000 // 30 секунд максимум
            });

            await Task.Delay(5000); // Даём React подгрузи

            // Если категория — симулируем клик (YouDo SPA, URL не меняется, но JS фильтрует)
            if (categoryId.HasValue && categoryId != 0)
            {
                var value = GetCategorySelector(categoryId.Value); // "Дизайн", "Курьерские услуги" и т.д.

                // СНИМАЕМ "Все категории" (на всякий случай)
                var rest = await page.EvaluateFunctionAsync<bool>(@"
        () => {
            const allLabel = Array.from(document.querySelectorAll('label.Checkbox_label__uNY3B'))
                .find(l => l.textContent.trim() === 'Все категории');
            if (allLabel) allLabel.click();
        }
    ");
                //if (rest)
                //    Console.WriteLine("Good");

                await Task.Delay(2000);

                // 2.КЛИКАЕМ ПО LABEL С НУЖНЫМ ТЕКСТОМ
    var clicked = await page.EvaluateFunctionAsync<bool>(@"
        (name) => {
            const labels = Array.from(document.querySelectorAll('label.Checkbox_label__uNY3B'));
            const targetLabel = labels.find(l => l.textContent.trim() === name);
            if (targetLabel) {
                targetLabel.click();
                return true;
            }
            return false;
        }", value);

                if (clicked)
                {
                    Console.WriteLine($"Категория YouDo value={value} выбрана");
                }
                else
                {
                    Console.WriteLine($"Не удалось найти или кликнуть категорию YouDo value={value}");
                }

                // Ждём подгрузки заказов — но НЕ падаем, если их нет
                try
                {
                    await page.WaitForSelectorAsync("li.TasksList_listItem__2Yurg", new WaitForSelectorOptions { Timeout = 12000 });
                    Console.WriteLine("Заказы в категории подгружены");
                }
                catch (WaitTaskTimeoutException)
                {
                    Console.WriteLine($"В категории YouDo value={value} пока нет заказов — возвращаем пустой список");
                    // Ничего не делаем — просто продолжаем, orders будет пустой
                }

                await Task.Delay(3000); // небольшая страховка
            }

            await Task.Delay(_rnd.Next(4000, 6000));

            var html = await page.GetContentAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Селектор по твоему HTML: li.TasksList_listItem__2Yurg
            var cards = doc.DocumentNode.SelectNodes("//li[contains(@class, 'TasksList_listItem')]");
            if (cards == null) return orders;

            foreach (var card in cards.Take(10))
            {
                try
                {
                    // ID из href="/t14257992"
                    var linkNode = card.SelectSingleNode(".//a[contains(@href, '/t')]");
                    if (linkNode == null) continue;
                    var href = linkNode.GetAttributeValue("href", "");
                    var idMatch = System.Text.RegularExpressions.Regex.Match(href, @"/t(\d+)");
                    if (!idMatch.Success) continue;
                    var taskId = long.Parse(idMatch.Groups[1].Value);

                    var title = HttpUtility.HtmlDecode(linkNode.InnerText.Trim().Replace("\n", " ").Trim());

                    // Бюджет: span nowrap с ₽
                    var budgetNode = card.SelectSingleNode(".//span[contains(@class, 'nowrap') and contains(., '₽')]");
                    var budget = budgetNode != null ? HttpUtility.HtmlDecode(budgetNode.InnerText.Trim()) : null;

                    // Адрес: div.TasksList_address__I6kQF
                    var addressNode = card.SelectSingleNode(".//div[contains(@class, 'TasksList_address')]");
                    var address = addressNode != null ? HttpUtility.HtmlDecode(addressNode.InnerText.Trim()) : null;

                    // Дата: div.TasksList_date__PQAJ4
                    var dateNode = card.SelectSingleNode(".//div[contains(@class, 'TasksList_date')]");
                    var startDate = dateNode != null ? HttpUtility.HtmlDecode(dateNode.InnerText.Trim()) : null;

                    // Описание: если есть, кликни на ссылку и парсь (опционально, для скорости — пропустим)
                    var description = ""; // Можно добавить page.GoToAsync(fullUrl) для детального парсинга, но медленно

                    var fullUrl = "https://youdo.com" + href;

                    orders.Add(new YoudoOrder(
                        TaskId: taskId,
                        Title: title,
                        Url: fullUrl,
                        Budget: budget,
                        Description: description,
                        Address: address,
                        StartDate: startDate,
                        ParsedAt: DateTime.UtcNow
                    ));
                }
                catch { continue; }
            }

            return orders;
        }

        // Селекторы для клика по категориям (YouDo SPA — кликай по тексту)
        private string GetCategorySelector(int catId) => catId switch
        {
            1 => "Курьерские услуги", // Используй page.ClickAsync("text=Название")
            2 => "Ремонт и строительство",
            3 => "Грузоперевозки",
            4 => "Уборка и помощь по хозяйству",
            5 => "Виртуальный помощник",
            6 => "Компьютерная помощь",
            7 => "Мероприятия и промоакции",
            8 => "Дизайн",
            9 => "Разработка ПО",
            10 => "Фото, видео и аудио",
            11 => "Установка и ремонт техники",
            12 => "Красота и здоровье",
            13 => "Ремонт цифровой техники",
            14 => "Юридическая и бухгалтерская помощь",
            15 => "Репетиторы и обучение",
            16 => "Ремонт транспорта",
            _ => ""
        };
    }
}
