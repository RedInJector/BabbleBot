using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using ShopifySharp;
using ShopifySharp.Filters;
using LiteDB;

namespace BabbleBot.Messagers;

internal class DirectMessageSender : Messager
{
    // LiteDB database for tracking redeemed orders
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<RedeemedOrder> _redeemedOrdersCollection;

    // Model to represent redeemed orders in the database
    private class RedeemedOrder
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public string Email { get; set; }
        public ulong DiscordUserId { get; set; }
        public DateTime RedeemedAt { get; set; }
    }

    public DirectMessageSender(Config config, DiscordSocketClient client) : base(config, client)
    {
        // Initialize LiteDB database
        _database = new LiteDatabase("./redemptions.db");
        _redeemedOrdersCollection = _database.GetCollection<RedeemedOrder>("redeemed_orders");

        // Create an index on OrderNumber and Email for faster lookups
        _redeemedOrdersCollection.EnsureIndex(x => x.OrderNumber);
        _redeemedOrdersCollection.EnsureIndex(x => x.Email);

        Client.Ready += Client_Ready;
        Client.SlashCommandExecuted += SlashCommandHandler;
    }
    
    public async Task Client_Ready()
    {
        // Babble Discord 974302302179557416
        // Test Discord 1270160076035850342
        const ulong BabbleGuild = 1270160076035850342;  
        var guild = Client.GetGuild(BabbleGuild);

        // Create the verify order slash command
        var verifyOrderCommand = new SlashCommandBuilder()
            .WithName("verify-order")
            .WithDescription("Verify your Babble order")
            .AddOption("order-number", ApplicationCommandOptionType.String, "Your order number", isRequired: true)
            .AddOption("email", ApplicationCommandOptionType.String, "Email used for the order", isRequired: true);

        try
        {
            // Create the slash command in the guild
            await guild.CreateApplicationCommandAsync(verifyOrderCommand.Build());
        }
        catch (ApplicationCommandException exception)
        {
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
            await Utils.Log(new LogMessage(LogSeverity.Critical, "Slash Commands", json));
        }
    }

    public async Task SlashCommandHandler(SocketSlashCommand command)
    {
        // Check if the command is our verify-order command
        if (command.Data.Name == "verify-order")
        {
            // Extract order number and email from command options
            var orderNumber = command.Data.Options.First(opt => opt.Name == "order-number").Value.ToString();
            var email = command.Data.Options.First(opt => opt.Name == "email").Value.ToString();

            // Verify the purchase
            var verificationResult = await VerifyPurchaseAsync(
                orderNumber, 
                email, 
                command.User.Id
            );

            // Respond to the command
            await command.RespondAsync(verificationResult, ephemeral: true);
        }
    }

    private async Task<string> VerifyPurchaseAsync(string confirmationNumber, string email, ulong discordUserId)
    {
        try
        {
            // First, check if the order has already been redeemed
            var existingRedemption = _redeemedOrdersCollection.FindOne(
                r => r.OrderNumber == confirmationNumber && 
                     r.Email == email
            );

            if (existingRedemption != null)
            {
                // Order already redeemed by another user
                return "❌ This order has already been redeemed.";
            }

            var service = new OrderService(Config.ShopifySite, Config.ShopifyToken);

            // Fetch orders with optional filters for performance
            var ordersResult = await service.ListAsync(new OrderListFilter
            {
                Status = "any", // Fetch all order statuses
                Fields = "id,name,email,financial_status" // Minimal required fields
            });

            // Match the order by confirmation number and email
            var matchingOrder = ordersResult.Items.FirstOrDefault(order =>
                order.Name.Equals(confirmationNumber, StringComparison.OrdinalIgnoreCase) &&
                order.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

            if (matchingOrder != null)
            {
                // Order found, mark as redeemed
                var redemption = new RedeemedOrder
                {
                    OrderNumber = confirmationNumber,
                    Email = email,
                    DiscordUserId = discordUserId,
                    RedeemedAt = DateTime.UtcNow
                };

                // Insert redemption record
                _redeemedOrdersCollection.Insert(redemption);

                return $"✅ Purchase verified! Order {matchingOrder.Name} for {matchingOrder.Email}, status: {matchingOrder.FinancialStatus}.";
            }
            else
            {
                return "❌ Purchase not found. Please ensure the order number and email are correct.";
            }
        }
        catch (Exception ex)
        {
            // Log the actual exception for debugging
            await Utils.Log(new LogMessage(
                LogSeverity.Error, 
                "Purchase Verification", 
                $"Verification error: {ex.Message}"
            ));

            return $"❌ An error occurred while verifying the purchase. Please try again.";
        }
    }

    // Dispose method to properly close the database connection
    public void Dispose()
    {
        _database?.Dispose();
    }
}