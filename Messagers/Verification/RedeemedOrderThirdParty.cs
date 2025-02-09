using LiteDB;

namespace BabbleBot.Messagers.Verification;

public class RedeemedOrderThirdParty
{
    // Use ObjectId as the internal ID type
    [BsonId] public ObjectId _id { get; set; }

    // Public property that converts ObjectId to int when needed
    [BsonIgnore]
    public int Id
    {
        get => _id.Timestamp;
        set => _id = new ObjectId((int)DateTime.UtcNow.Ticks, 0, 0, value);
    }

    public string OrderNumber { get; set; }
    public string ThirdPartyName { get; set; } = string.Empty;
    public string DiscordUserId { get; set; } = "0";
    public DateTime RedeemedAt { get; set; } = DateTime.MinValue;
}