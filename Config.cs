namespace BabbleBot;

internal class Config
{
    public string DiscordToken { get; set; } = "";
    public string ShopifyToken { get; set; } = "";
    public string ShopifySite { get; } = "4fac42-f0.myshopify.com";
    public int FuzzThreshold { get; } = 60;
}
