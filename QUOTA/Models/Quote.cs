namespace QUOTA.Models;

public class Quote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public string Author { get; set; } = "Unknown";
    public string Genre { get; set; } = "General";
    public string MusicUrl { get; set; } = string.Empty;
}
