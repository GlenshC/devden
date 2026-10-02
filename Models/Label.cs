namespace DevDen.Models;

public record Label(
    Guid Id,
    string Name,
    string ColorToken
);

public static class LabelColors
{
    // Fixed palette of 6 warm and neutral tokens only
    public static readonly IReadOnlyList<LabelColorInfo> WarmTokens = new List<LabelColorInfo>
    {
        new("crimson", "Crimson", "bg-crimson/15 text-crimson border-crimson/30"),
        new("coral", "Coral", "bg-coral/15 text-coral border-coral/30"),
        new("amber", "Amber", "bg-amber/15 text-amber border-amber/30"),
        new("sand", "Sand", "bg-[#e2cb9b]/15 text-[#e2cb9b] border-[#e2cb9b]/30"),
        new("ash", "Ash", "bg-[#d1d5db]/15 text-[#d1d5db] border-[#d1d5db]/30"),
        new("muted", "Muted", "bg-[#9c9c9d]/15 text-[#9c9c9d] border-[#9c9c9d]/30")
    };

    public static string GetClass(string? token)
    {
        var found = WarmTokens.FirstOrDefault(t => t.Token.Equals(token, StringComparison.OrdinalIgnoreCase));
        return found?.BadgeClass ?? "bg-muted/15 text-muted border-muted/30";
    }
}

public record LabelColorInfo(string Token, string Name, string BadgeClass);
