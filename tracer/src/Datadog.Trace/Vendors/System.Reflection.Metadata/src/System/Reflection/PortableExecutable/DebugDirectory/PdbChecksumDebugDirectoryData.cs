//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0032
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8620, CS8714, CS8762, CS8765, CS8766, CS8767, CS8768, CS8769, CS8612, CS8629, CS8774
// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.PdbChecksumDebugDirectoryData
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72

using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;

#nullable enable
namespace Datadog.Trace.VendoredMicrosoftCode.System.Reflection.PortableExecutable
{
  internal readonly struct PdbChecksumDebugDirectoryData
  {
    /// <summary>Checksum algorithm name.</summary>
    public string AlgorithmName { get; }

    /// <summary>
    /// GUID (Globally Unique Identifier) of the associated PDB.
    /// </summary>
    public ImmutableArray<byte> Checksum { get; }

    internal PdbChecksumDebugDirectoryData(string algorithmName, ImmutableArray<byte> checksum)
    {
      this.AlgorithmName = algorithmName;
      this.Checksum = checksum;
    }
  }
}
