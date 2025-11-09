using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TgAgentAI.Models
{
    public record ContentPlanItem(string Date, string Rubric, string Title, string Description);
    public record ContentPlan(List<ContentPlanItem> Items);
}
