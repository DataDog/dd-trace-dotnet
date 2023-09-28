// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.HandleComparer
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    public sealed class HandleComparer : 
    IEqualityComparer<Handle>,
    IComparer<Handle>,
    IEqualityComparer<EntityHandle>,
    IComparer<EntityHandle>
  {

    #nullable disable
    private static readonly HandleComparer s_default = new HandleComparer();

    private HandleComparer()
    {
    }


    #nullable enable
    public static HandleComparer Default => HandleComparer.s_default;

    public bool Equals(Handle x, Handle y) => x.Equals(y);

    public bool Equals(EntityHandle x, EntityHandle y) => x.Equals(y);

    public int GetHashCode(Handle obj) => obj.GetHashCode();

    public int GetHashCode(EntityHandle obj) => obj.GetHashCode();

    /// <summary>Compares two handles.</summary>
    /// <remarks>
    /// The order of handles that differ in kind and are not <see cref="T:System.Reflection.Metadata.EntityHandle" /> is undefined.
    /// Returns 0 if and only if <see cref="M:System.Reflection.Metadata.HandleComparer.Equals(System.Reflection.Metadata.Handle,System.Reflection.Metadata.Handle)" /> returns true.
    /// </remarks>
    public int Compare(Handle x, Handle y) => Handle.Compare(x, y);

    /// <summary>Compares two entity handles.</summary>
    /// <remarks>
    /// Returns 0 if and only if <see cref="M:System.Reflection.Metadata.HandleComparer.Equals(System.Reflection.Metadata.EntityHandle,System.Reflection.Metadata.EntityHandle)" /> returns true.
    /// </remarks>
    public int Compare(EntityHandle x, EntityHandle y) => EntityHandle.Compare(x, y);
  }
}
