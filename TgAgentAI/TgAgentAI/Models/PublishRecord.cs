using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TgAgentAI.Models
{
    public record PublishRecord(
        DateTime PublishedAt,
        string Rubric,
        string Title,
        string Status);
}
