using Discord;

namespace BabbleBot;

internal class Utils
{
    public static string LogPath;

    internal static Task Log(LogMessage msg)
    {
        var text = msg.ToString();
        Console.WriteLine(new LogMessage(LogSeverity.Info, "Logger", text));
        File.AppendAllText(LogPath, text + Environment.NewLine);
        return Task.CompletedTask;
    }
}
