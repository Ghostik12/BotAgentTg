using VkBotParser.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace VkBotParser.Parsers
{
    public class FlParser
    {
        private readonly Random _rnd = new();
        private readonly IProxyProvider _proxy;

        public FlParser(IProxyProvider proxy) { _proxy = proxy; }

        public record FlOrder(
            string Title,
            string Url,
            string? Budget,
            string? Description,
            long ProjectId,
            DateTime ParsedAt);

        public async Task<List<FlOrder>> GetNewOrdersAsync(int? categoryId = null)
        {
            var orders = new List<FlOrder>();

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

            var url = categoryId.HasValue && categoryId.Value != 0
                ? $"https://www.fl.ru/projects/category/{GetCategorySlug(categoryId.Value)}/"
                : "https://www.fl.ru/projects/";

            //await page.GoToAsync(url, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });
            await GoToWithRetry(page, url);
            await Task.Delay(_rnd.Next(4000, 8000)); // имитация человека

            var html = await page.GetContentAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Главный селектор — все карточки проектов
            var cards = doc.DocumentNode.SelectNodes("//div[starts-with(@id, 'project-item') and contains(@class, 'b-post')]");
            if (cards == null) return orders;

            foreach (var card in cards.Take(10))
            {
                try
                {
                    // ID проекта из атрибута id (самый надёжный способ)
                    var idMatch = Regex.Match(card.Id, @"\d+");
                    if (!idMatch.Success) continue;
                    var projectId = long.Parse(idMatch.Value);

                    // Заголовок и ссылка
                    var linkNode = card.SelectSingleNode(".//a[contains(@id, 'prj_name_') or contains(@href, '/projects/')]");
                    if (linkNode == null) continue;

                    var title = HttpUtility.HtmlDecode(linkNode.InnerText.Trim());
                    var href = linkNode.GetAttributeValue("href", "");
                    var fullUrl = href.StartsWith("http") ? href : "https://www.fl.ru" + href;

                    // Бюджет
                    var budgetNode = card.SelectSingleNode(".//div[contains(@class, 'b-post__price')]//span");
                    var budget = budgetNode != null
                        ? HttpUtility.HtmlDecode(budgetNode.InnerText.Trim())
                        : null;

                    // Описание
                    var descNode = card.SelectSingleNode(".//div[contains(@class, 'b-post__txt') and not(contains(@class, 'b-post__foot'))]");
                    var description = descNode != null
                        ? HttpUtility.HtmlDecode(descNode.InnerText.Trim())
                        : null;

                    orders.Add(new FlOrder(
                        Title: title,
                        Url: fullUrl,
                        Budget: budget,
                        Description: description,
                        ProjectId: projectId,
                        ParsedAt: DateTime.UtcNow
                    ));
                }
                catch
                {
                    continue; // молча пропускаем битые карточки
                }
            }

            return orders;
        }

        private async Task GoToWithRetry(IPage page, string url, int maxRetries = 3)
        {
            for (int i = 1; i <= maxRetries; i++)
            {
                try
                {
                    await page.GoToAsync(url, new NavigationOptions
                    {
                        Timeout = 60000, // 60 сек
                        WaitUntil = new[] { WaitUntilNavigation.Networkidle2 } // ← КЛЮЧЕВОЕ ИЗМЕНЕНИЕ!
                    });
                    return;
                }
                catch (NavigationException ex)
                {
                    if (i == maxRetries) throw;
                    await Task.Delay(10000); // пауза перед повтором
                }
            }
        }

        private string GetCategorySlug(int id) => id switch
        {
            1 => "saity",
            2 => "dizajn",
            3 => "prodvizhenie-saitov-seo",
            4 => "programmirovanie",
            5 => "reklama-marketing",
            6 => "teksty",
            7 => "mobile",
            8 => "3d-grafika",
            9 => "konsalting",
            10 => "ai-iskusstvenniy-intellekt",
            11 => "risunki-i-illustracii",
            12 => "crypto-i-blockchain",
            13 => "inzhiniring",
            14 => "audio-video-photo",
            15 => "messengers",
            16 => "animaciya",
            17 => "marketplace-management",
            18 => "avtomatizaciya-biznesa",
            19 => "games",
            20 => "firmennyi-stil",
            21 => "brauzery",
            22 => "socialnye-seti",
            23 => "internet-magaziny",
            _ => ""
        };
    }
}
