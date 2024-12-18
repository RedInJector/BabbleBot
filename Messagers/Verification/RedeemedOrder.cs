using Babble_Bot.Enums;

namespace BabbleBot.Messagers;

internal partial class VerificationMessageSender
{
    private class RedeemedOrder
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public string Email { get; set; }
        public ulong DiscordUserId { get; set; }
        public DateTime RedeemedAt { get; set; }
        public ProductTier HighestTier { get; set; }
        public List<string> OrderedProducts { get; set; }
    }
}