// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ICustomAttributeTypeProvider`1
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public interface ICustomAttributeTypeProvider<TType> : 
    ISimpleTypeProvider<TType>,
    ISZArrayTypeProvider<TType>
  {
    /// <summary>
    /// Gets the TType representation for <see cref="T:System.Type" />.
    /// </summary>
    TType GetSystemType();

    /// <summary>
    /// Returns true if the given type represents <see cref="T:System.Type" />.
    /// </summary>
    bool IsSystemType(TType type);

    /// <summary>
    /// Get the type symbol for the given serialized type name.
    /// The serialized type name is in so-called "reflection notation" (i.e. as understood by <see cref="M:System.Type.GetType(System.String)" />.)
    /// </summary>
    /// <exception cref="T:System.BadImageFormatException">The name is malformed.</exception>
    TType GetTypeFromSerializedName(string name);

    /// <summary>
    /// Gets the underlying type of the given enum type symbol.
    /// </summary>
    /// <exception cref="T:System.BadImageFormatException">The given type symbol does not represent an enum.</exception>
    PrimitiveTypeCode GetUnderlyingEnumType(TType type);
  }
}
