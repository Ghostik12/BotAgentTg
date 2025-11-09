using GenerativeAI.Types;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TgAgentAI.Models;

namespace TgAgentAI.Services
{
    public class ContentService : IContentService
    {
        private readonly GenerativeAI _gemini;
        private readonly IConfiguration _config;

        public ContentService(GenerativeAI gemini, IConfiguration config)
        {
            _gemini = gemini;
            _config = config;
        }

        public async Task GenerateContentPlanAsync()
        {
            var model = _gemini.GetGenerativeModel("gemini-1.5-flash");
            var prompt = """
        Составь контент-план на 7 дней. Рубрики: новости, репортажи, кейсы, разборы, до/после.
        По 1 посту в день. Стиль: честно, профессионально, без глянца.
        JSON: [{"date":"2025-11-10","rubric":"кейс","title":"Фундамент на болоте","description":"3 дня, 15 м³ бетона"}]
        """;

            var response = await model.GenerateContentAsync(prompt);
            var json = response.Candidates.First().Content.Parts.First().Text;
            // Сохраняем в Sheets — см. ниже
        }

        public async Task<List<PostDraft>> GenerateDraftsAsync(string mediaPath, string mediaFileId, string rubric)
        {
            var model = _gemini.GetGenerativeModel("gemini-1.5-flash");

            var imagePart = File.Exists(mediaPath)
                ? new Part { InlineData = new() { MimeType = "image/jpeg", Data = Convert.ToBase64String(await File.ReadAllBytesAsync(mediaPath)) } }
                : null;

            var parts = new List<Part>
        {
            new() { Text = $"""
                Рубрика: {rubric}
                Фото с объекта. Сгенерируй 3 варианта поста.
                Стиль: честно, профессионально, без воды. Цифры, факты.
                JSON: [{"title":"...", "body":"...", "hashtags":"..."}]
                """ }
        };
            if (imagePart != null) parts.Insert(0, imagePart);

            var response = await model.GenerateContentAsync(new GenerateContentRequest
            {
                Contents = [new() { Role = "user", Parts = parts.ToArray() }]
            });

            var json = response.Candidates.First().Content.Parts.First().Text;
            var drafts = System.Text.Json.JsonSerializer.Deserialize<List<PostDraftDto>>(json)!;

            return drafts.Select((d, i) => new PostDraft(
                Title: d.Title,
                Body: d.Body,
                Hashtags: d.Hashtags,
                MediaFileId: mediaFileId,
                Rubric: rubric,
                ScheduledAt: DateTime.Today.AddDays(i + 1).AddHours(10)
            )).ToList();
        }
    }

    record PostDraftDto(string Title, string Body, string Hashtags);
}
