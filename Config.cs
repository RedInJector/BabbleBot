namespace BabbleBot;

internal class Config
{
    public string DiscordToken { get; set; } = "";
    public string ShopifySite { get; set; } = "4fac42-f0.myshopify.com";
    public string ShopifyToken { get; set; } = "";
    public int FuzzThreshold { get; set; } = 60;
}
