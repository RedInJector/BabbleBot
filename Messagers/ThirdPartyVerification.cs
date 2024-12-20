using System.Drawing;
using System.Text;
using BabbleBot.Messagers.ThirdParty;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using QRCodeDecoderLibrary;

namespace BabbleBot.Messagers;

internal class ThirdPartyVerification : Messager
{
    private static readonly QRDecoder Decoder = new();
    private static readonly HttpClient HttpClient = new();
    
    public ThirdPartyVerification(Config config, DiscordSocketClient client) : base(config, client)
    {
        Client.Ready += Client_Ready;
        Client.SlashCommandExecuted += SlashCommandHandler;
    }

    private async Task Client_Ready()
    { 
        var command = new SlashCommandBuilder()
            .WithName("verify-order-qr")
            .WithDescription("Upload an image of a QR code to verify your order.")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("image")
                .WithDescription("QR Code image")
                .WithRequired(true)
                .WithType(ApplicationCommandOptionType.Attachment));
        
        try
        {
            // Make this command Babble Discord only
            var guild = Client.GetGuild(BabbleGuild);
            await guild.CreateApplicationCommandAsync(command.Build());
        }
        catch (ApplicationCommandException exception)
        {
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
            await Utils.Log(new LogMessage(LogSeverity.Critical, "Order Verification - QR", json));
        }
    }
    
    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        if (command.Data.Name != "verify-order-qr")
            return;

        // Expect an attachment option
        if (command.Data.Options.FirstOrDefault() is not { Value: Attachment attachment })
        {
            await command.RespondAsync("Please upload an image!");
            return;
        }

        try
        {
            // Download the attachment as a stream
            await using var imageStream = await HttpClient.GetStreamAsync(attachment.Url);

            // Decode the first (only) QR Code in this image
            var qrData = Decoder.ImageDecoder(new Bitmap(imageStream)).First();
            
            // Convert the bytes to text
            var qrText = Encoding.UTF8.GetString(qrData);
            
            // Convert the text to a ThirdPartyDataModel
            var thirdPartyData = JsonConvert.DeserializeObject<ThirdPartyDataModel>(qrText)!;

            if (!string.IsNullOrEmpty(qrText))
            {
                await command.RespondAsync($"QR Code content: `{thirdPartyData.Manufacturer}` - `{thirdPartyData.OrderId}`");
            }
            else
            {
                await command.RespondAsync("Failed to decode the QR code. Please ensure it's a valid QR code.");
            }
        }
        catch (Exception ex)
        {
            await command.RespondAsync("An error occurred while processing the image.");
            Console.WriteLine(ex);
        }
    }
}