using System;

namespace SmokeTests;

public enum InstallType
{
    DebX64,
    RpmX64,
    TarX64,
    TarMuslX64,
    DebArm64,
    RpmArm64,
    TarArm64,
}

public static class InstallTypeExtensions
{
    public static string GetInstallCommand(this InstallType type) => type switch
    {
        InstallType.DebX64 => "dpkg -i ./datadog-dotnet-apm*_amd64.deb",
        InstallType.RpmX64 => "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
        InstallType.TarX64 => "tar -C /opt/datadog -xzf ./datadog-dotnet-apm*.tar.gz",
        InstallType.TarMuslX64 => "tar -C /opt/datadog -xzf ./datadog-dotnet-apm*-musl.tar.gz",
        InstallType.DebArm64 => "dpkg -i ./datadog-dotnet-apm_*_arm64.deb",
        InstallType.RpmArm64 => "rpm -Uvh ./datadog-dotnet-apm*-1.aarch64.rpm",
        InstallType.TarArm64 => "tar -C /opt/datadog -xzf ./datadog-dotnet-apm*.arm64.tar.gz",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
