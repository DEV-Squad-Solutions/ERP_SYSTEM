namespace MiniErp.Infrastructure.Persistence;

public sealed class EntityIdentifierSequence
{
    public string Scope { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public int LastNumber { get; set; }
}
