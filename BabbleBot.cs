using BabbleBot.Helpers;
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



    public BabbleBot(string logPath, string configFile)
    {
        // Generate a unique log file name based on the current date and time
        Directory.CreateDirectory(logPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var logFilePath = Path.Combine(logPath, $"bot_{timestamp}.log");

        var services = new ServiceCollection();
        services.AddLogging((loggingBuilder) => loggingBuilder
                    .AddConsole()
                    .AddDebug()
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddFile(Path.Combine(logPath, "latest.log"), append: false));

        services.AddSingleton<Config>(provider => { 
            var logger = provider.GetService<ILoggerFactory>()!.CreateLogger<BabbleBot>();
            var config = ConfigLoader.ResolveConfig(configFile, logger);
            if (config == null)
            {
                Environment.Exit(0); // no config, nothing to do
            }
            return config;
        });

        services.AddSingleton<DiscordSocketConfig>(DiscordSocketFactory.CreateSocketConfig);
        services.AddSingleton<DiscordSocketClient>(DiscordSocketFactory.CreateSocketClient);

        services.AddSingleton<ChatMessageSender>();
        services.AddSingleton<VerificationMessageSender>();
        services.AddSingleton<SlashCommandSender>();

        ServiceProvider = services.BuildServiceProvider()!;

        _client = ServiceProvider.GetService<DiscordSocketClient>()!;
        _config = ServiceProvider.GetService<Config>()!;

        ServiceProvider.GetService<ChatMessageSender>();
        ServiceProvider.GetService<VerificationMessageSender>();
        ServiceProvider.GetService<SlashCommandSender>();
    }

    public async Task MainAsync()
    {
        await _client.LoginAsync(TokenType.Bot, _config.DiscordToken);
        await _client.StartAsync();

        // Block this task until the program is closed.
        await Task.Delay(-1);
    }
}
