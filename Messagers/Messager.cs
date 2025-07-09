using Discord.WebSocket;
using FuzzySharp;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BabbleBot.Messagers;

internal abstract class Messager
{
    public Config Config { get; set; }
    public DiscordSocketClient Client { get; set; }
    public Dictionary<string, ResponseMessage> Responses;
    protected ILogger Logger;


    public static ResponseMessage DefaultResponse = new()
    {
        Messages = new[]
        {
            new Message { Content = "Sorry, I don't have help information for that command." }
        }
    };

    public Messager(Config config, DiscordSocketClient client, ILogger<Messager> logger)
    {
        Config = config;
        Client = client;
        Logger = logger;

        Responses = new Dictionary<string, ResponseMessage>();
        LoadResponses();
    }

    protected Dictionary<string, ResponseMessage>? LoadResponses()
    {
        const string ResponsesPath = "responses.json";
        const string DefaultIdentifier = "default";

        try
        {
            var json = File.ReadAllText(ResponsesPath);
            var responses = JsonConvert.DeserializeObject<Dictionary<string, ResponseMessage>>(json)!;

            return responses;
        }
        catch (Exception ex)
        {
            Logger.LogCritical("Error loading responses: {}", ex.Message);
        }
        // Preload values
        if (Responses.TryGetValue(DefaultIdentifier, out var defaultResponse))
        {
            DefaultResponse = defaultResponse;
        }

        return null;
    }

    protected ResponseMessage GetHelpResponse(string command)
    {
        const string DefaultIdentifier = "default";

        if (Responses.TryGetValue(command, out var response))
        {
            return response;
        }
        else
        {
            // Slow path: Try a fuzzy search
            foreach (var kvPair in Responses)
            {
                if (kvPair.Key == DefaultIdentifier)
                {
                    continue;
                }
                int ratio = Fuzz.Ratio(command, kvPair.Key);
                if (ratio > Config.FuzzThreshold)
                {
                    return kvPair.Value;
                }
            }
        }
        return DefaultResponse;
    }
}
