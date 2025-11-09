using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TgAgentAI.Models;

namespace TgAgentAI.Services
{
    public interface IContentService
    {
        Task GenerateContentPlanAsync();
        Task<List<PostDraft>> GenerateDraftsAsync(string mediaPath, string mediaFileId, string rubric);
    }
}
