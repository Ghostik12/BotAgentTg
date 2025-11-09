using Microsoft.Extensions.Configuration;
using Notion.Client;
using TgAgentAI.Models;

namespace TgAgentAI.Services
{
    public class NotionService
    {
        private readonly INotionClient _client;
        private readonly string _dbId;

        public NotionService(INotionClient client, IConfiguration config)
        {
            _client = client;
            _dbId = config["Notion:DatabaseId"]!;
        }

        public async Task LogPublicationAsync(PublishRecord record)
        {
            var page = new PagesCreateParameters
            {
                Parent = new DatabaseParentInput { DatabaseId = _dbId },
                Properties = new Dictionary<string, PropertyValue>
                {
                    ["Дата"] = new DatePropertyValue { Date = new Date { Start = record.PublishedAt } },
                    ["Рубрика"] = new SelectPropertyValue { Select = new SelectOption { Name = record.Rubric } },
                    ["Название"] = new TitlePropertyValue { Title = new List<RichTextBase> { new RichTextBase { PlainTextT = new Text { Content = record.Title } } } },
                    ["Статус"] = new SelectPropertyValue { Select = new SelectOption { Name = record.Status } }
                }
            };
            await _client.Pages.CreateAsync(page);
        }
    }
}
