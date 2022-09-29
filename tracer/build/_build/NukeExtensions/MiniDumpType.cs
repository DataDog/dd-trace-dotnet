/// <summary>
/// See https://docs.microsoft.com/en-us/dotnet/core/diagnostics/dumps for details
/// </summary>
public enum MiniDumpType
{
    Default = 0,
    MiniDumpNormal = 1,
    MiniDumpWithPrivateReadWriteMemory = 2,
    MiniDumpFilterTriage = 3,
    MiniDumpWithFullMemory = 4,
}
