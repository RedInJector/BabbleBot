using BabbleBot.Messagers;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NReco.Logging.File;
using System.Reflection;

namespace BabbleBot;

internal class BabbleBot
{
    internal static ServiceProvider ServiceProvider { get; private set; }
    private readonly DiscordSocketClient _client;
    private readonly Config _config;
    private readonly ILogger<BabbleBot> _logger;
    
    public BabbleBot(string logPath, string configFile)
    {
        // Generate a unique log file name based on the current date and time
        Directory.CreateDirectory(logPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var logFilePath = Path.Combine(logPath, $"bot_{timestamp}.log");

        ServiceProvider = new ServiceCollection()
            .AddLogging((loggingBuilder) => loggingBuilder
                .AddConsole()
                .AddDebug()
                .SetMinimumLevel(LogLevel.Debug)
                .AddFile(Path.Combine(logPath, "latest.log"), append: false))
            .BuildServiceProvider();
        _logger = ServiceProvider.GetService<ILoggerFactory>()!.CreateLogger<BabbleBot>();

        var discordSocketConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.All,
            UseInteractionSnowflakeDate = false, // Don't timeout if an order lookup takes more than 3 seconds
        };

        _client = new DiscordSocketClient(discordSocketConfig);
        _client.SetStatusAsync(UserStatus.Online);
        _client.Log += DiscordClientLogged;

        if (!File.Exists(configFile))
        {
            // Create default config
            _config = new Config();
            File.WriteAllText(configFile, JsonConvert.SerializeObject(_config, Formatting.Indented));
            _logger.LogCritical("Config", "Config not found! Please assign a valid token.");
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
                // Find constructor that takes Config, DiscordSocketClient and ILogger
                var constructor = type.GetConstructor(new[] { typeof(Config), typeof(DiscordSocketClient), typeof(ILogger) });
                if (constructor != null)
                {
                    var messager = (Messager)constructor.Invoke(new object[] { _config, _client, _logger });
                    _logger.LogInformation($"Initialized {type.Name}");
                }
                else
                {
                    _logger.LogWarning($"Failed to find appropriate constructor for {type.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to initialize {type.Name}: {ex.Message}");
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

    private async Task DiscordClientLogged(Discord.LogMessage message)
    {
        _logger.LogInformation(message.ToString());
    }
}
