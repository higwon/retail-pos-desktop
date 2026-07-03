namespace RetailPOS.Infrastructure.Configuration;

public sealed class LocalDatabaseOptions
{
    public const string SectionName = "LocalDatabase";

    public string DatabasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RetailPOS",
        "retail-pos.db");
}
