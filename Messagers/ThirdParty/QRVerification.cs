//using BabbleBot.Messagers.ThirdParty;
//using Discord;
//using Discord.Net;
//using Discord.WebSocket;
//using Microsoft.Extensions.Logging;
//using Newtonsoft.Json;
//using OpenCvSharp;

//namespace BabbleBot.Messagers;

//internal class QRVerification: Messager
//{
//    private static readonly HttpClient HttpClient = new();
//    private readonly QRCodeDetector Detector = new();

//    public QRVerification(Config config, DiscordSocketClient client, ILogger logger) : base(config, client, logger)
//    {
//        Client.Ready += Client_Ready;
//        Client.SlashCommandExecuted += SlashCommandHandler;
//    }

//    private async Task Client_Ready()
//    { 
//        var command = new SlashCommandBuilder()
//            .WithName("verify-order-qr")
//            .WithDescription("Upload an image of a QR code to verify your order.")
//            .AddOption(new SlashCommandOptionBuilder()
//                .WithName("image")
//                .WithDescription("QR Code image")
//                .WithRequired(true)
//                .WithType(ApplicationCommandOptionType.Attachment));
        
//        try
//        {
//            // Make this command Babble Discord only
//            var guild = Client.GetGuild(BabbleGuild);
//            await guild.CreateApplicationCommandAsync(command.Build());
//        }
//        catch (ApplicationCommandException exception)
//        {
//            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
//            Logger.LogCritical("Order Verification - QR", json);
//        }
//    }
    
//    private async Task SlashCommandHandler(SocketSlashCommand command)
//    {
//        if (command.Data.Name != "verify-order-qr")
//            return;

//        // Expect an attachment option
//        if (command.Data.Options.FirstOrDefault() is not { Value: Attachment attachment })
//        {
//            await command.RespondAsync("Please upload an image!");
//            return;
//        }

//        try
//        {
//            // Download the attachment as a stream
//            var imageBytes = await HttpClient.GetByteArrayAsync(attachment.Url);

//            // Decode
//            using Mat rawImage = Mat.FromImageData(imageBytes);
//            var qrText = Detector.DetectAndDecode(rawImage, out _);

//            if (string.IsNullOrEmpty(qrText))
//            {
//                await command.RespondAsync("Could not find a QR code in the image, or its text was empty/invalid.");
//                return;
//            }

//            // Convert the text to a ThirdPartyDataModel
//            try
//            {
//                var thirdPartyData = JsonConvert.DeserializeObject<QRDataModel>(qrText)!;
//                await command.RespondAsync($"QR Code content: `{thirdPartyData.Manufacturer}` - `{thirdPartyData.OrderId}`");
//            }
//            catch (Exception)
//            {
//                await command.RespondAsync("Found a QR code, but it didn't contain any Babble product information! Please try again.");
//            }            
//        }
//        catch (Exception)
//        {
//            await command.RespondAsync("An error occurred while decoding the image.");
//        }
//    }
//}