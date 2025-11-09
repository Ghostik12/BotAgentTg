using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TgAgentAI.Models
{
    public record PostDraft(
        string Title,
        string Body,
        string Hashtags,
        string MediaFileId,
        string Rubric,
        DateTime ScheduledAt);
}
