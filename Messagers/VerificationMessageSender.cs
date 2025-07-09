using Discord;
using Discord.Net;
using Discord.WebSocket;
using LiteDB;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ShopifySharp;
using ShopifySharp.Filters;
using BabbleBot.Enums;
using BabbleBot.Helpers;
using BabbleBot.Messagers.Verification;

namespace BabbleBot.Messagers;

internal class VerificationMessageSender : Messager, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly LiteDatabase _databaseThirdParty;
    private readonly ILiteCollection<RedeemedOrder> _redeemedOrdersCollection;
    private readonly ILiteCollection<RedeemedOrderThirdParty> _redeemedOrdersCollectionThirdParty;

    private readonly Dictionary<ProductTier, ulong> _tierRoleIds = new()
    {
        { ProductTier.Tier1, 1319029761267470408 },
        { ProductTier.Tier2, 1319029937532833882 },
        { ProductTier.Tier3, 1319029967094284338 }
    };
    
    private readonly Dictionary<string, ulong> _tierRoleIdsThirdParty = new()
    {
        { "PREL", 1338197055935348756 },
    };

    private readonly Dictionary<ProductTier, List<string>> _productTierPrefixes = new()
    {
        {
            ProductTier.Tier1,
            new List<string>
            {
                "Babble Mouth Tracker V1 Assembled PCB"
            }
        },
        {
            ProductTier.Tier2,
            new List<string>
            {
                "Babble Mouth Tracker V1 Base Kit",
            }
        },
        {
            ProductTier.Tier3,
            new List<string>
            {
                "Babble Mouth Tracker V1 Supporter Kit",
            }
        }
    };

    public VerificationMessageSender(Config config, DiscordSocketClient client, ILogger<VerificationMessageSender> logger) : base(config, client, logger)
    {
        const string redemptions = "./redemptions.db";
        const string redemptionsThirdParty = "./redemptionsThirdParty.db";
        const string redeemedOrders = "redeemed_orders";
        
        // First-time third party CSV setup. This will overwrite a DB if it exists!
        // ImportOrders("third-party.csv", redemptionsThirdParty, redeemedOrders);
        
        Client.Ready += Client_Ready;
        Client.SlashCommandExecuted += SlashCommandHandler;

        // Initialize LiteDB databases
        _database = new LiteDatabase(redemptions);
        _redeemedOrdersCollection = _database.GetCollection<RedeemedOrder>(redeemedOrders);
        
        _databaseThirdParty = new LiteDatabase(redemptionsThirdParty);
        _redeemedOrdersCollectionThirdParty = _databaseThirdParty.GetCollection<RedeemedOrderThirdParty>(redeemedOrders);

        // Create indexes for faster lookups
        _redeemedOrdersCollection.EnsureIndex(x => x.OrderNumber);
        _redeemedOrdersCollection.EnsureIndex(x => x.Email);
        _redeemedOrdersCollection.EnsureIndex(x => x.DiscordUserId);
        
        _redeemedOrdersCollectionThirdParty.EnsureIndex(x => x.OrderNumber);
        _redeemedOrdersCollectionThirdParty.EnsureIndex(x => x.ThirdPartyName);
    }
    
    private void ImportOrders(string csvFilePath, string connectionString, string collectionName)
    {
        var typedMappings = new Dictionary<string, (string NewName, Func<string, BsonValue> Converter)>
        {
            // Map Hex Code to OrderNumber
            { "Hex Code", ("OrderNumber", value => new BsonValue(value)) },
            
            // Map Manufacturer to ThirdPartyName
            { "Manufacturer", ("ThirdPartyName", value => new BsonValue(value.Trim())) },
            
            // Static/default values
            { "Id", ("Id", _ => new BsonValue(0)) },
            { "DiscordUserId", ("DiscordUserId", _ => new BsonValue("0")) },  // Store Discord ID as string
            { "RedeemedAt", ("RedeemedAt", _ => new BsonValue(DateTime.UtcNow)) }
        };

        var importer = new CsvToLiteDbImporter(connectionString, collectionName);
        importer.ImportCsvWithTypedColumns(csvFilePath, typedMappings);
    }
    
    private async Task Client_Ready()
    { 
        var command = new SlashCommandBuilder()
            .WithName("verify-order")
            .WithDescription("Verify your Babble order.")
            .AddOption("order-number", ApplicationCommandOptionType.String, "Your order number", isRequired: true)
            .AddOption("email", ApplicationCommandOptionType.String, "Email used for the order", isRequired: true)
            .WithContextTypes(InteractionContextType.BotDm, InteractionContextType.PrivateChannel);
        
        var thirdPartyCommand = new SlashCommandBuilder()
            .WithName("verify-order-third-party")
            .WithDescription("Verify your third party Babble order.")
            .AddOption("order-number", ApplicationCommandOptionType.String, "Your order number", isRequired: true)
            .WithContextTypes(InteractionContextType.BotDm, InteractionContextType.PrivateChannel);

        try
        {
            // Make this command Babble Discord only
            var guild = Client.GetGuild(Config.guildId);
            await guild.CreateApplicationCommandAsync(command.Build());
            await guild.CreateApplicationCommandAsync(thirdPartyCommand.Build());
        }
        catch (HttpException exception)
        {
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
            Logger.LogCritical("{}", json);
        }
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        if (command.Data.Name == "verify-order")
        { 
            var orderNumber = command.Data.Options.First(opt => opt.Name == "order-number").Value.ToString()!;
            var email = command.Data.Options.First(opt => opt.Name == "email").Value.ToString()!;

            // Verify the purchase
            var verificationResult = await VerifyPurchaseAsync(
                orderNumber,
                email,
                command.User.Id
            );
            // Respond to the command
            await command.RespondAsync(verificationResult, ephemeral: true);
        }
        
        if (command.Data.Name == "verify-order-third-party")
        {
            // Extract order number and email from command options
            var orderNumber = command.Data.Options.First(opt => opt.Name == "order-number").Value.ToString()!;

            // Verify the purchase
            var verificationResult = await VerifyPurchaseAsyncThirdParty(
                orderNumber, 
                command.User.Id
            );

            // Respond to the command
            await command.RespondAsync(verificationResult, ephemeral: true);
        }
    }

    // Extract order number and email from command options
    private async Task<string> VerifyPurchaseAsync(string confirmationNumber, string email, ulong discordUserId)
    {
        try
        {
            // Check for existing redemption
            var existingRedemption = _redeemedOrdersCollection.FindOne(
                r => r.OrderNumber == confirmationNumber &&
                     r.Email == email
            );

            if (existingRedemption != null)
            {
                return "❌ This order has already been redeemed.";
            }

            var service = new OrderService(Config.ShopifySite, Config.ShopifyToken);
            Order matchingOrder = null;
            const int pageSize = 250;
            long sinceId = 0;
            int ordersSearched = 0;

            // First, get the earliest orders to start our search
            var response = await service.ListAsync(new OrderListFilter
            {
                Limit = 1,
                Status = "any",
                Fields = "id"
            });

            if (!response.Items.Any())
            {
                return "❌ No orders found in the store.";
            }

            while (matchingOrder == null)
            {
                // Fetch next batch of orders
                var ordersResult = await service.ListAsync(new OrderListFilter
                {
                    Limit = pageSize,
                    SinceId = sinceId,
                    Status = "any",
                    Fields = "id,name,email,financial_status,line_items"
                });

                var orders = ordersResult.Items.ToList();

                // If no more orders to process, break
                if (!orders.Any())
                {
                    break;
                }

                ordersSearched += orders.Count;

                // Try to find matching order in current page
                matchingOrder = orders.FirstOrDefault(order =>
                    order.Name.Equals("#" + confirmationNumber, StringComparison.OrdinalIgnoreCase) &&
                    order.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

                if (matchingOrder != null)
                {
                    Logger.LogInformation("Found order after searching through {} orders.", ordersSearched);     
                    break;
                }

                // Update sinceId for next page (using the last order's ID)
                sinceId = orders.Last().Id.Value;

                // Optional: Add delay to respect rate limits
                await Task.Delay(100); // 100ms delay between requests
            }

            if (matchingOrder != null)
            {
                var orderedProducts = matchingOrder.LineItems.Select(item => item.Name).ToList();
                var highestTier = DetermineHighestTier(orderedProducts);

                var redemption = new RedeemedOrder
                {
                    OrderNumber = confirmationNumber,
                    Email = email,
                    DiscordUserId = discordUserId,
                    RedeemedAt = DateTime.UtcNow,
                    HighestTier = highestTier,
                    OrderedProducts = orderedProducts
                };

                _redeemedOrdersCollection.Insert(redemption);

                // Attempt to assign role
                string roleAssignmentResult = await AssignTierRole(discordUserId, highestTier);

                return $"✅ Purchase verified! Order {matchingOrder.Name} for {matchingOrder.Email}\n" +
                       $"- Status: {StringExtensions.FirstCharToUpper(matchingOrder.FinancialStatus)}\n" +
                       $"- Products: {string.Join(", ", orderedProducts)}\n" +
                       $"- Role Assignment: {roleAssignmentResult}\n";
            }
            else
            {
                return $"❌ Purchase not found. Please ensure the order number and email are correct.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Verification error: {}", ex.Message);

            return $"❌ An error occurred while verifying the purchase. Please try again.";
        }
    }
    
    private async Task<string> VerifyPurchaseAsyncThirdParty(string confirmationNumber, ulong discordUserId)
    {
        try
        {
            // Check for existing redemption
            var existingRedemptionThirdParty = _redeemedOrdersCollectionThirdParty.FindOne(
                r => r.OrderNumber == confirmationNumber
            );
            
            if (existingRedemptionThirdParty == null)
            {
                return "❌ This order does not exist.";
            }

            var hasUserRedeemed = ulong.Parse(existingRedemptionThirdParty.DiscordUserId) != 0;
            if (hasUserRedeemed)
            {
                return "❌ This order has already been redeemed.";
            }

            existingRedemptionThirdParty.DiscordUserId = discordUserId.ToString();
            existingRedemptionThirdParty.RedeemedAt = DateTime.Now;
            _redeemedOrdersCollectionThirdParty.Update(existingRedemptionThirdParty);

            // Attempt to assign role
            string roleAssignmentResult = await AssignTierRoleThirdParty(discordUserId, existingRedemptionThirdParty.ThirdPartyName);

            return $"✅ Purchase verified! Third-party order with {existingRedemptionThirdParty.ThirdPartyName}\n" +
                   $"- Order Number: {existingRedemptionThirdParty.OrderNumber}\n" +
                   $"- Role Assignment: {roleAssignmentResult}\n";
        }
        catch (Exception ex)
        {
            Logger.LogError("Verification error: {}", ex.Message);

            return $"❌ An error occurred while verifying the purchase. Please try again.";
        }
    }

    private async Task<string> AssignTierRole(ulong discordUserId, ProductTier tier)
    {
        try
        {
            var guild = Client.GetGuild(Config.guildId);

            if (guild == null)
            {
                return "❌ Could not find the guild";
            }

            var guildUser = guild.GetUser(discordUserId);

            if (guildUser == null)
            {
                return "❌ User not found in the guild";
            }

            // Check if we have a role for this tier
            if (!_tierRoleIds.TryGetValue(tier, out ulong roleId))
            {
                return "❌ No role found for this tier";
            }

            var role = guild.GetRole(roleId);

            if (role == null)
            {
                return "❌ Role not found";
            }

            // Remove any existing tier roles
            var existingTierRoles = guildUser.Roles
                .Where(r => _tierRoleIds.ContainsValue(r.Id))
                .ToList();

            foreach (var existingRole in existingTierRoles)
            {
                await guildUser.RemoveRoleAsync(existingRole);
            }

            // Add new tier role
            await guildUser.AddRoleAsync(role);

            return $"Assigned \"{role.Name}\" role successfully";
        }
        catch (Exception ex)
        {
            Logger.LogError("Role assignment error: {}", ex.Message);

            return $"❌ Error assigning role: {ex.Message}";
        }
    }

    private async Task<string> AssignTierRoleThirdParty(ulong discordUserId, string thidPartyName)
    {
        try
        {
            var guild = Client.GetGuild(Config.guildId);

            if (guild == null)
            {
                return "❌ Could not find the guild";
            }

            var guildUser = guild.GetUser(discordUserId);

            if (guildUser == null)
            {
                return "❌ User not found in the guild";
            }

            // Check if we have a role for this tier
            if (!_tierRoleIdsThirdParty.TryGetValue(thidPartyName, out ulong roleId))
            {
                return "❌ No role found for this tier";
            }

            var role = guild.GetRole(roleId);

            if (role == null)
            {
                return "❌ Role not found";
            }

            // Remove any existing tier roles
            var existingTierRoles = guildUser.Roles
                .Where(r => _tierRoleIds.ContainsValue(r.Id))
                .ToList();

            foreach (var existingRole in existingTierRoles)
            {
                await guildUser.RemoveRoleAsync(existingRole);
            }

            // Add new tier role
            await guildUser.AddRoleAsync(role);

            return $"Assigned \"{role.Name}\" role successfully";
        }
        catch (Exception ex)
        {
            Logger.LogError(
               "Role assignment error: {}", ex.Message
            );

            return $"❌ Error assigning role: {ex.Message}";
        }
    }
    
    private ProductTier DetermineHighestTier(List<string> products)
    {
        // Create a mapping to track the highest tier found for each product
        var productTiers = new Dictionary<string, ProductTier>();

        foreach (var product in products)
        {
            var highestProductTier = ProductTier.None;

            // Check each tier's prefixes
            foreach (var tierPrefix in _productTierPrefixes)
            {
                // Check if any prefix matches the product name
                if (tierPrefix.Value.Any(prefix => product.Contains(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    highestProductTier = tierPrefix.Key > highestProductTier ? tierPrefix.Key : highestProductTier;
                }
            }

            productTiers[product] = highestProductTier;
        }

        // Return the highest tier found across all products
        return productTiers.Values.DefaultIfEmpty(ProductTier.None).Max();
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}