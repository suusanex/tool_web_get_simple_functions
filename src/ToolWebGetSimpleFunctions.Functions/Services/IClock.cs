namespace ToolWebGetSimpleFunctions.Functions.Services;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
