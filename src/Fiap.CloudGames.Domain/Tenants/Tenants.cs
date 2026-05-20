namespace Fiap.CloudGames.Domain.Tenants;

public static class Tenants
{
    public const string Fiap = "FIAP";
    public const string Alura = "Alura";
    public const string Pm3 = "PM3";
    public const string Default = Fiap;

    public static readonly IReadOnlySet<string> Known =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Fiap, Alura, Pm3 };

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Default;
        return Known.Contains(raw) ? raw : Default;
    }
}
