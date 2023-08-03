public enum DockerDependencyType
{
    /// <summary>
    /// Does not require Docker to run sample
    /// </summary>
    None,

    /// <summary>
    /// Requires Docker on all platform to run sample
    /// </summary>
    All,

    /// <summary>
    /// Requires Docker on all platforms except Windows to run samples.
    /// </summary>
    LinuxAndMac,
}
