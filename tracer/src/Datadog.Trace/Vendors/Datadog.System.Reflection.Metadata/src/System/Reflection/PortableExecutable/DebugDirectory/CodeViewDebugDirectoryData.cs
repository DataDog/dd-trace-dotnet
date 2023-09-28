// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.CodeViewDebugDirectoryData
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.PortableExecutable
{
  public readonly struct CodeViewDebugDirectoryData
  {
    /// <summary>
    /// GUID (Globally Unique Identifier) of the associated PDB.
    /// </summary>
    public Guid Guid { get; }

    /// <summary>
    /// Iteration of the PDB. The first iteration is 1. The iteration is incremented each time the PDB content is augmented.
    /// </summary>
    public int Age { get; }

    /// <summary>
    /// Path to the .pdb file containing debug information for the PE/COFF file.
    /// </summary>
    public string Path { get; }

    internal CodeViewDebugDirectoryData(Guid guid, int age, string path)
    {
      this.Path = path;
      this.Guid = guid;
      this.Age = age;
    }
  }
}
