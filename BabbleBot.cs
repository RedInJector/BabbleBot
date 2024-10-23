using BabbleBot.Messagers;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace BabbleBot;

internal class BabbleBot
{
    private DiscordSocketClient _client;
    private InteractionService _interactionService;
    private Config _config;
    
    public BabbleBot(string logPath, string configFile)
    {
        // Generate a unique log file name based on the current date and time
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        Directory.CreateDirectory(logPath);
        var logFilePath = Path.Combine(logPath, $"bot_{timestamp}.log");
        Utils.LogPath = logFilePath;

        var discordSocketconfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
        };

        _client = new DiscordSocketClient(discordSocketconfig);
        _client.SetStatusAsync(UserStatus.Online);
        _client.Log += Utils.Log;

        if (!File.Exists(configFile))
        {
            // Create default config
            _config = new Config();
            File.WriteAllText(configFile, JsonConvert.SerializeObject(_config, Formatting.Indented));
            Utils.Log(new LogMessage(LogSeverity.Critical, "Config", "Config not found! Please assign a valid token."));
            return;
        }

        _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configFile))!;

        ChatMessageSender chatMessageSender = new ChatMessageSender(_config, _client);
        SlashCommandSender slashCommandSender = new SlashCommandSender(_config, _client);
    }
    
    public async Task MainAsync()
    {
        await _client.LoginAsync(TokenType.Bot, _config.Token);
        await _client.StartAsync();

        // Block this task until the program is closed.
        await Task.Delay(-1);
    }
}
