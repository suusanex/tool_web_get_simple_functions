namespace ToolWebGetSimpleFunctions.Functions.Options;

public sealed class SourceCollectionOptions
{
    public const string SectionName = "SourceCollection";

    public string OfficialNewsUrl { get; set; } = "https://rakuspa.com/kanda/news/";

    public string FacilityName { get; set; } = "RAKU SPA 1010 神田";

    public string[] FacilityAliases { get; set; } =
    [
        "RAKU SPA 1010 神田",
        "らくスパ 1010 神田",
        "らくスパ1010神田",
        "RAKU SPA 1010 KANDA"
    ];

    public int HttpTimeoutSeconds { get; set; } = 20;

    public int MaxPageBytes { get; set; } = 1024 * 1024;

    public int MaxLinkedPagesPerRequest { get; set; } = 8;

    public int MaxCandidateArticles { get; set; } = 20;
}
