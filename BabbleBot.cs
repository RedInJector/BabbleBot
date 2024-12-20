using System.Reflection;
using BabbleBot.Messagers;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace BabbleBot;

internal class BabbleBot
{
    private readonly DiscordSocketClient _client;
    private readonly Config _config;
    
    public BabbleBot(string logPath, string configFile)
    {
        // Generate a unique log file name based on the current date and time
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        Directory.CreateDirectory(logPath);
        var logFilePath = Path.Combine(logPath, $"bot_{timestamp}.log");
        Utils.LogPath = logFilePath;

        var discordSocketConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.All,
            UseInteractionSnowflakeDate = false, // Don't timeout if an order lookup takes more than 3 seconds
        };

        _client = new DiscordSocketClient(discordSocketConfig);
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
        InitializeMessagers();
    }
    
    private void InitializeMessagers()
    {
        // Get all types that inherit from Messager
        var messagerTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Messager)));

        foreach (var type in messagerTypes)
        {
            try
            {
                // Find constructor that takes Config and DiscordSocketClient
                var constructor = type.GetConstructor(new[] { typeof(Config), typeof(DiscordSocketClient) });
                if (constructor != null)
                {
                    var messager = (Messager)constructor.Invoke(new object[] { _config, _client });
                    Utils.Log(new LogMessage(LogSeverity.Info, "Messager", $"Initialized {type.Name}"));
                }
                else
                {
                    Utils.Log(new LogMessage(LogSeverity.Warning, "Messager", 
                        $"Failed to find appropriate constructor for {type.Name}"));
                }
            }
            catch (Exception ex)
            {
                Utils.Log(new LogMessage(LogSeverity.Error, "Messager", 
                    $"Failed to initialize {type.Name}: {ex.Message}"));
            }
        }
    }
    
    public async Task MainAsync()
    {
        await _client.LoginAsync(TokenType.Bot, _config.DiscordToken);
        await _client.StartAsync();

        // Block this task until the program is closed.
        await Task.Delay(-1);
    }
}
