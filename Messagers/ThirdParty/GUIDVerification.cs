//using BabbleBot.Messagers.ThirdParty;
//using BabbleBot;
//using Discord.Net;
//using Discord.WebSocket;
//using Discord;
//using Microsoft.Extensions.Logging;
//using Newtonsoft.Json;
//using OpenCvSharp;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace BabbleBot.Messagers.ThirdParty;

//internal class GUIDVerification : Messager
//{
//    public GUIDVerification(Config config, DiscordSocketClient client, ILogger logger) : base(config, client, logger)
//    {
//        Client.Ready += Client_Ready;
//        Client.SlashCommandExecuted += SlashCommandHandler;
//    }

//    private async Task Client_Ready()
//    {
//        var command = new SlashCommandBuilder()
//            .WithName("verify-order-id")
//            .WithDescription("Type in a verification code to verify your order.")
//            .AddOption("order-number", ApplicationCommandOptionType.String, "Your order number", isRequired: true);

//        try
//        {
//            // Make this command Babble Discord only
//            var guild = Client.GetGuild(BabbleGuild);
//            await guild.CreateApplicationCommandAsync(command.Build());
//        }
//        catch (ApplicationCommandException exception)
//        {
//            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
//            Logger.LogCritical(json);
//        }
//    }

//    private async Task SlashCommandHandler(SocketSlashCommand command)
//    {
//        if (command.Data.Name != "verify-order-id")
//            return;

//        // TODO!
//    }
//}
