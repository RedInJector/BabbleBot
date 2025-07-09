using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BabbleBot
{
    internal class DiscordSocketFactory
    {
#pragma warning disable IDE0060 // Remove unused parameter. DI Service Provider wants to have this argument present
        public static DiscordSocketConfig CreateSocketConfig(IServiceProvider provider)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            var discordSocketConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All,
                UseInteractionSnowflakeDate = false, // Don't timeout if an order lookup takes more than 3 seconds
            };
            return discordSocketConfig;
        }

        public static DiscordSocketClient CreateSocketClient(IServiceProvider provider)
        {
            var logger = provider.GetRequiredService<ILogger<DiscordSocketClient>>();
            var discordSocketConfig = provider.GetRequiredService<DiscordSocketConfig>();

            DiscordSocketClient client;
            client = new DiscordSocketClient(discordSocketConfig);
            client.SetStatusAsync(UserStatus.Online);
            client.Log += async (message) =>
            {
                logger.LogInformation("{}", message.ToString());
                await Task.CompletedTask;
            };

            return client;
        }

    }
}
