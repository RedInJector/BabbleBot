using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BabbleBot.Messagers;

internal class ChatMessageSender : Messager
{
    private const string HelpCommandPrefix = "!";

    public ChatMessageSender(Config config, DiscordSocketClient client, ILogger logger) : base(config, client, logger)
    {
        Client.MessageReceived += MessageReceivedAsync;
        LoadResponses();
    }

    private async Task MessageReceivedAsync(SocketMessage message)
    {
        const string ReloadCommand = "reload";

        if (message.Author.IsBot)
            return;

        // Admin commands
        if (ADMIN_WHITELIST_ID.Contains(message.Author.Id))
        {
            if (message.Content.ToLower().Trim() == $"{HelpCommandPrefix}{ReloadCommand}") // !reload
            {
                LoadResponses();
                await message.Channel.SendMessageAsync("Reloaded responses!");
                return;
            }
        }

        if (message.Content.StartsWith(HelpCommandPrefix))
        {
            var command = message.Content.Substring(HelpCommandPrefix.Length).Trim();
            var response = GetHelpResponse(command.ToLower());
            await SendResponseMessageAsync(message.Channel, response);
        }
    }

    private async Task SendResponseMessageAsync(ISocketMessageChannel channel, ResponseMessage message)
    {
        const string AttachmentsPath = "attachments";

        foreach (var msg in message.Messages)
        {
            if (msg.Content.Trim().Length > 0 && msg.Attachment.Length == 0)
            {
                await channel.SendMessageAsync(msg.Content);
            }
            if (msg.Attachment.Length > 0)
            {
                var attachmentPath = Path.GetFullPath(Path.Combine(AttachmentsPath, msg.Attachment));
                if (File.Exists(attachmentPath))
                {
                    await channel.SendFileAsync(attachmentPath, msg.Content.Length > 0 ? msg.Content : null);
                }
            }
        }
    }
}
