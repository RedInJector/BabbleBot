using Babble_Bot;
using Discord;
using Discord.WebSocket;
using FuzzySharp;
using Newtonsoft.Json;

namespace BabbleBot;

internal class Program {

    private static readonly ulong[] ADMIN_WHITELIST_ID = new [] {
        346338830011596800UL,
        199983920639377410UL,
        282909752042717194UL,
    };
     
    private const string ConfigPath = "config.json";
    private const string ResponsesPath = "responses.json";
    private const string HelpCommand = "!";
    private static ResponseMessage DefaultResponse = new ResponseMessage() { Messages = new [] { new Message() { Content = "Sorry, I don't have help information for that command." } } };
    private static DiscordSocketClient _client;
    private Dictionary<string, ResponseMessage> _responses = new();
    private readonly string LogFilePath;
    private Config _config;

    public Program() {
        // Generate a unique log file name based on the current date and time
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        LogFilePath = Path.Combine(LogDirectory, $"bot_{timestamp}.log");
    }

    public static void Main(string[] args) => new Program().
        MainAsync().
        GetAwaiter().
        GetResult();

    public async Task MainAsync() {
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
        };
        _client = new DiscordSocketClient(config);

        _client.Log += Log;
        _client.MessageReceived += MessageReceivedAsync;
        LoadResponses();

        if ( !File.Exists(ConfigPath) ) {
            // Create default config
            _config = new Config();
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(_config, Formatting.Indented));
            await Log(new LogMessage(LogSeverity.Critical, "Config", "Config not found! Please assign a valid token."));
            return;
        }

        _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigPath))!;

        await _client.LoginAsync(TokenType.Bot, _config.Token);
        await _client.StartAsync();

        // Block this task until the program is closed.
        await Task.Delay(-1);
    }

    private Task Log(LogMessage msg) {
        Console.WriteLine(msg.ToString());

        // Append the log message to the log file
        File.AppendAllText(LogFilePath, msg.ToString() + Environment.NewLine);

        return Task.CompletedTask;
    }

    private async Task MessageReceivedAsync(SocketMessage message) {
        if ( message.Author.IsBot )
            return;

        // Admin commands
        if ( ADMIN_WHITELIST_ID.Contains(message.Author.Id) ) {
            if (message.Content.ToLower().Trim() == $"{HelpCommand}reload" ) {
                LoadResponses();
                await message.Channel.SendMessageAsync("Reloaded responses!");
                return;
            }
        }

        if ( message.Content.StartsWith(HelpCommand) ) {
            var command = message.Content.Substring(HelpCommand.Length).Trim();
            var response = GetHelpResponse(command.ToLower());
            await SendResponseMessage(message.Channel, response);
        }
    }

    private void LoadResponses() {
        var json = File.ReadAllText(ResponsesPath);
        _responses = JsonConvert.DeserializeObject<Dictionary<string, ResponseMessage>>(json)!;

        // Preload values
        if ( _responses.TryGetValue("default", out var defaultResponse) ) {
            DefaultResponse = defaultResponse;
        }
    }

    private ResponseMessage GetHelpResponse(string command) {
        if ( _responses.TryGetValue(command, out var response) ) {
            return response;
        } else {
            // Slow path: Try a fuzzy search
            foreach (var kvPair in _responses) {
                if ( kvPair.Key == "default" ) {
                    continue;
                }
                int ratio = Fuzz.Ratio(command, kvPair.Key);
                if (ratio > _config.FuzzThreshold) {
                    return kvPair.Value;
                }
            }
        }
        return DefaultResponse;
    }

    private async Task SendResponseMessage(ISocketMessageChannel channel, ResponseMessage message) {
        foreach (var msg in message.Messages) {
            if ( msg.Content.Trim().Length > 0 && msg.Attachment.Length == 0) {
                await channel.SendMessageAsync(msg.Content);
            }
            if ( msg.Attachment.Length > 0 ) {
                string attachmentPath = Path.GetFullPath(Path.Combine("attachments", msg.Attachment));
                if ( File.Exists(attachmentPath) ) {
                    await channel.SendFileAsync(attachmentPath, msg.Content.Length > 0 ? msg.Content : null);
                }
            }
        }
    }
}
