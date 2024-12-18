using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace BabbleBot.Messagers;

internal class SlashCommandSender : Messager
{
    public SlashCommandSender(Config config, DiscordSocketClient client) : base(config, client)
    {
        Client.Ready += Client_Ready;
        Client.SlashCommandExecuted += SlashCommandHandler;
    }

    public async Task Client_Ready()
    {
        foreach (var response in Responses)
        {
            var command = new SlashCommandBuilder()
                .WithName(response.Key)
                .WithDescription("Ask the BabbleBot a question!")
                .WithContextTypes(
                    InteractionContextType.Guild,
                    InteractionContextType.BotDm,
                    InteractionContextType.PrivateChannel );

            try
            {
                await Client.CreateGlobalApplicationCommandAsync(command.Build());
            }
            catch (ApplicationCommandException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                await Utils.Log(new LogMessage(LogSeverity.Critical, "Slash Commands", json));
            }
        }
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        if (command.Data.Name == "verify-order") return;

        var response = GetHelpResponse(command.Data.Name);
        
        foreach (var m in response.Messages)
        {
            if (!string.IsNullOrEmpty(m.Attachment))
            {
                await command.RespondWithFileAsync(m.Content, "Attachment", m.Content);
            }
            else
            {
                await command.RespondAsync(m.Content);
            }
        }
    }
}
