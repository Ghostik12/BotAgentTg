using GenerativeAI;
using GenerativeAI.Types;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using TgAgentAI.Models;

namespace TgAgentAI.Services
{
    public class ContentService : IContentService
    {
        private readonly GenerativeModel _gemini;
        private readonly IConfiguration _config;

        public ContentService(GenerativeModel gemini, IConfiguration config)
        {
            _gemini = gemini;
            _config = config;
        }

        public async Task GenerateContentPlanAsync()
        {
            var prompt = """
        Составь контент-план на 7 дней для Telegram-канала строительной компании.
        Рубрики: новости, репортажи, кейсы, разборы, до/после. По 1 посту в день.
        Стиль: честно, профессионально, без глянца. Используй цифры, факты.
        Верни строго JSON: [{"date":"2025-11-10","rubric":"кейс","title":"Фундамент на болоте","description":"3 дня, 15 м³ бетона"}]
        """;

            var request = new GenerateContentRequest
            {
                Contents = new[] { new Content { Role = "user", Parts = new[] { new Part { Text = prompt } } } },
                GenerationConfig = new GenerationConfig { Temperature = 0.7f, MaxOutputTokens = 1000 }
            };

            var response = await _gemini.GenerateContentAsync(request);
            var json = response.Candidates.First().Content.Parts.First().Text;

            // Очищаем от markdown (типа ```json)
            json = json.Trim().Replace("```json", "").Replace("```", "").Trim();

            // Парсим и сохраняем (здесь добавь вызов Sheets)
            var plan = JsonSerializer.Deserialize<ContentPlan>(json)!;
            // await _sheets.AppendPlanAsync(plan.Items); // Если нужно
        }

        public async Task<List<PostDraft>> GenerateDraftsAsync(string mediaPath, string mediaFileId, string rubric)
        {
            var prompt = $"""
        Рубрика: {rubric}
        Сгенерируй 3 варианта поста для Telegram на основе фото/видео с объекта.
        Стиль: честно, профессионально, без глянца и воды. Цифры, факты, вызовы.
        Верни строго JSON: ["title":"...", "body":"Текст до 200 слов", "hashtags":"#стройка #фундамент"]
        """;

            var parts = new List<Part>
        {
            new Part { Text = prompt }
        };

            // Добавляем медиа, если файл существует (мультимодальный ввод)
            if (File.Exists(mediaPath))
            {
                var bytes = await File.ReadAllBytesAsync(mediaPath);
                var mimeType = Path.GetExtension(mediaPath).ToLower() switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".mp4" => "video/mp4",
                    _ => "image/jpeg"
                };
                parts.Insert(0, new Part
                {
                    InlineData = new GenerativeContentBlob
                    {
                        MimeType = mimeType,
                        Data = bytes  // Base64 не нужен — SDK сам конвертирует
                    }
                });
            }

            var request = new GenerateContentRequest
            {
                Contents = new[] { new Content { Role = "user", Parts = parts.ToArray() } },
                GenerationConfig = new GenerationConfig { Temperature = 0.7f, MaxOutputTokens = 800 }
            };

            var response = await _gemini.GenerateContentAsync(request);
            var json = response.Candidates.First().Content.Parts.First().Text;

            // Очищаем от markdown
            json = json.Trim().Replace("```json", "").Replace("```", "").Trim();

            var draftsDto = JsonSerializer.Deserialize<List<PostDraftDto>>(json)!;

            return draftsDto.Select((d, i) => new PostDraft(
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
