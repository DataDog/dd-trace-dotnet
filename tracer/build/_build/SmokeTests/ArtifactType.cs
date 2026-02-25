using System;

namespace SmokeTests;

public enum ArtifactType
{
    LinuxX64,
    LinuxMuslX64,
    LinuxArm64,
}

public static class ArtifactTypeExtensions
{
    public static string GetArtifactName(this ArtifactType type) => type switch
    {
        ArtifactType.LinuxX64 => "linux-packages-linux-x64",
        ArtifactType.LinuxMuslX64 => "linux-packages-linux-musl-x64",
        ArtifactType.LinuxArm64 => "linux-packages-linux-arm64",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
