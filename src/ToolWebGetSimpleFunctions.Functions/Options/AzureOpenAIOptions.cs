namespace ToolWebGetSimpleFunctions.Functions.Options;

public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;

    public string DeploymentName { get; set; } = string.Empty;

    public string ApiVersion { get; set; } = "2024-10-21";

    public string ApiKey { get; set; } = string.Empty;

    public int HttpTimeoutSeconds { get; set; } = 30;

    public int MaxInputCharacters { get; set; } = 80000;

    public double Temperature { get; set; } = 0;

    public int MaxTokens { get; set; } = 2000;
}
