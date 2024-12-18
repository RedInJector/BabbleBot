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
        // Babble Discord 974302302179557416
        // Test Discord 1270160076035850342
        const ulong BabbleGuild = 1270160076035850342;  
        var guild = Client.GetGuild(BabbleGuild);

        foreach (var response in Responses)
        {
            var guildCommand = new SlashCommandBuilder();
            guildCommand.WithName(response.Key);
            guildCommand.WithDescription("Ask the BabbleBot a question!");

            try
            {
                await guild.CreateApplicationCommandAsync(guildCommand.Build());
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
