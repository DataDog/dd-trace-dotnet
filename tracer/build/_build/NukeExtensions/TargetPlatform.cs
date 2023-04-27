/// <summary>
/// Essentially the same as <see cref="Nuke.Common.Tools.MSBuild.MSBuildTargetPlatform"/>
/// but with the extra values we need, as a real enum.
/// These names should match the MSBuildTargetPlatform equivalents for easier interoperability
/// </summary>
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
public enum TargetPlatform
{
    MSIL, 
    x86,
    x64,
    arm64,
    arm64ec,
}

