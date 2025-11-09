using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TgAgentAI.Services;

namespace TgAgentAI.Jobs
{
    public class ContentPlanJob
    {
        private readonly IContentService _content;
        private readonly GoogleSheetsService _sheets;

        public ContentPlanJob(IContentService content, GoogleSheetsService sheets)
        {
            _content = content;
            _sheets = sheets;
        }

        public async Task GenerateWeeklyPlan()
        {
            await _content.GenerateContentPlanAsync();
        }
    }
}
