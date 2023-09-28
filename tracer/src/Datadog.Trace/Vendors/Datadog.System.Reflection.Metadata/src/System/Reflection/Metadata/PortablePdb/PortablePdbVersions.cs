// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.PortablePdbVersions
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  internal static class PortablePdbVersions
  {
    /// <summary>
    /// Version of Portable PDB format emitted by the writer by default. Metadata version string.
    /// </summary>
    internal const string DefaultMetadataVersion = "PDB v1.0";
    /// <summary>
    /// Version of Portable PDB format emitted by the writer by default.
    /// </summary>
    internal const ushort DefaultFormatVersion = 256;
    /// <summary>Minimal supported version of Portable PDB format.</summary>
    internal const ushort MinFormatVersion = 256;
    /// <summary>
    /// Minimal supported version of Embedded Portable PDB blob.
    /// </summary>
    internal const ushort MinEmbeddedVersion = 256;
    /// <summary>
    /// Version of Embedded Portable PDB blob format emitted by the writer by default.
    /// </summary>
    internal const ushort DefaultEmbeddedVersion = 256;
    /// <summary>
    /// Minimal version of the Embedded Portable PDB blob that the current reader can't interpret.
    /// </summary>
    internal const ushort MinUnsupportedEmbeddedVersion = 512;
    internal const uint DebugDirectoryEmbeddedSignature = 1111773261;
    internal const ushort PortableCodeViewVersionMagic = 20557;

    internal static uint DebugDirectoryEntryVersion(ushort portablePdbVersion) => 1347223552U | (uint) portablePdbVersion;

    internal static uint DebugDirectoryEmbeddedVersion(ushort portablePdbVersion) => 16777216U | (uint) portablePdbVersion;

    internal static string Format(ushort version)
    {
      int num = (int) version >> 8;
      string str1 = num.ToString();
      num = (int) version & (int) byte.MaxValue;
      string str2 = num.ToString();
      return str1 + "." + str2;
    }
  }
}
