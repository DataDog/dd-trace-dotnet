namespace SmokeTests;

public record SmokeTestScenario(
    SmokeTestCategory Category,
    string ShortName,
    string PublishFramework,
    string RuntimeTag,
    bool IsLinuxContainer = true,
    bool RunCrashTest = true,
    bool IsNoop = false)
{
    public string JobName { get; } = $"{ShortName}_{RuntimeTag.Replace('.', '_')}";
    public string FullName => $"{Category}_{JobName}";
    public string DockerTag => $"dd-trace-dotnet/{JobName}-tester";
}