using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TgAgentAI.Models;

namespace TgAgentAI.Services
{
    public class GoogleSheetsService
    {
        private readonly SheetsService _sheets;
        private readonly string _spreadsheetId;

        public GoogleSheetsService(SheetsService sheets, IConfiguration config)
        {
            _sheets = sheets;
            _spreadsheetId = config["GoogleSheets:SpreadsheetId"]!;
        }

        public async Task AppendPlanAsync(List<ContentPlanItem> items)
        {
            var values = items.Select(i => new object[] { i.Date, i.Rubric, i.Title, i.Description }).ToList();
            var body = new ValueRange { Values = values.Cast<IList<object>>().ToList() };
            await _sheets.Spreadsheets.Values.Append(body, _spreadsheetId, "Plan!A:D")
                .SetValueInputOption("RAW").ExecuteAsync();
        }
    }
}
