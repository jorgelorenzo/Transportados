namespace Transportados.Persistence.Seeding;

public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";

    public bool Enabled { get; set; } = true;
    public bool SeedTransportados { get; set; } = true;
}
