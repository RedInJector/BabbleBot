using Discord.WebSocket;
using FuzzySharp;
using Newtonsoft.Json;

namespace BabbleBot.Messagers;

internal abstract class Messager
{
    public Config Config { get; set; }
    public DiscordSocketClient Client { get; set; }
    public Dictionary<string, ResponseMessage> Responses;

    /// <summary>
    /// Discord user IDs for dfgHiatus, RamesTheGeneric, and SummerSky
    /// </summary>
    public static readonly ulong[] ADMIN_WHITELIST_ID = new[] {
        346338830011596800UL,
        199983920639377410UL,
        282909752042717194UL,
    };

    public static ResponseMessage DefaultResponse = new ResponseMessage()
    {
        Messages = new[] 
        {
            new Message() { Content = "Sorry, I don't have help information for that command." }
        }
    };

    public Messager(Config config, DiscordSocketClient client)
    {
        Config = config;
        Client = client;
        LoadResponses();
    }

    protected void LoadResponses()
    {
        const string ResponsesPath = "responses.json";
        const string DefaultIdentifier = "default";

        var json = File.ReadAllText(ResponsesPath);
        Responses = JsonConvert.DeserializeObject<Dictionary<string, ResponseMessage>>(json)!;

        // Preload values
        if (Responses.TryGetValue(DefaultIdentifier, out var defaultResponse))
        {
            DefaultResponse = defaultResponse;
        }
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
