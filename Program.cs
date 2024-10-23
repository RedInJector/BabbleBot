namespace BabbleBot;

internal class Program
{
    public static void Main(string[] args) => new BabbleBot("Logs", "config.json").
       MainAsync().
       GetAwaiter().
       GetResult();
}
