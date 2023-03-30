public enum PlatformRequirement
{
    /// <summary>
    /// Build default supported platforms
    /// </summary>
    Default = 0,
    
    /// <summary>
    /// Build all available platforms. On Windows x64, this includes arm64 builds
    /// </summary>
    ForceWindowsArm64 = 1,

    /// <summary>
    /// Build only the single platform specified in <see cref="Build.TargetPlatform"/>
    /// </summary>
    Single = 2,
}
