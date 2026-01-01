using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VkBotParser;
using VkBotParser.Db;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<VkBot>();

var options = new DbContextOptionsBuilder<KworkBotDbContext>()
    .UseSqlite("Data Source=bot.db")
    .Options;

using var db = new KworkBotDbContext(options);

var vkBot = new VkBot(logger, db);
await vkBot.RunAsync("ТВОЙ_ACCESS_TOKEN_ИЗ_VK");