using Babble_Bot;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace BabbleBot;

internal class Program {
    private const string ResponsesPath = "responses.json";
    private const string HelpCommand = "!help";
    private static string DefaultResponse = "Sorry, I don't have help information for that command.";
    private static DiscordSocketClient _client;
    private Dictionary<string, string> _responses = new();
    private readonly string LogFilePath;

    public Program() {
        // Generate a unique log file name based on the current date and time
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        LogFilePath = $"bot_{timestamp}.log";
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

        var token = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"))!.Token;

        await _client.LoginAsync(TokenType.Bot, token);
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

        if ( message.Content.StartsWith("!help") ) {
            var command = message.Content.Substring(HelpCommand.Length).Trim();
            var response = GetHelpResponse(command);
            await message.Channel.SendMessageAsync(response);
        }
    }

    private void LoadResponses() {
        var json = File.ReadAllText(ResponsesPath);
        _responses = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)!;

        // Preload values
        if ( _responses.TryGetValue("default", out var defaultResponse) ) {
            DefaultResponse = defaultResponse;
        }
    }

    private string GetHelpResponse(string command) {
        if ( _responses.TryGetValue(command.ToLower(), out var response) ) {
            return response;
        }
        return DefaultResponse;
    }
}
