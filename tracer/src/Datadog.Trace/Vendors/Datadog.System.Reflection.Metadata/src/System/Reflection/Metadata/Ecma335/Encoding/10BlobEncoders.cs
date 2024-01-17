﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ScalarEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct ScalarEncoder
  {
    public BlobBuilder Builder { get; }

    public ScalarEncoder(BlobBuilder builder) => this.Builder = builder;

    /// <summary>
    /// Encodes <c>null</c> literal of type <see cref="T:System.Array" />.
    /// </summary>
    public void NullArray() => this.Builder.WriteInt32(-1);

    /// <summary>Encodes constant literal.</summary>
    /// <param name="value">
    /// Constant of type
    /// <see cref="T:System.Boolean" />,
    /// <see cref="T:System.Byte" />,
    /// <see cref="T:System.SByte" />,
    /// <see cref="T:System.Int16" />,
    /// <see cref="T:System.UInt16" />,
    /// <see cref="T:System.Int32" />,
    /// <see cref="T:System.UInt32" />,
    /// <see cref="T:System.Int64" />,
    /// <see cref="T:System.UInt64" />,
    /// <see cref="T:System.Single" />,
    /// <see cref="T:System.Double" />,
    /// <see cref="T:System.Char" /> (encoded as two-byte Unicode character),
    /// <see cref="T:System.String" /> (encoded as SerString), or
    /// <see cref="T:System.Enum" /> (encoded as the underlying integer value).
    /// </param>
    /// <exception cref="T:System.ArgumentException">Unexpected constant type.</exception>
    public void Constant(object? value)
    {
      switch (value)
      {
        case string str:
        case null:
            str = null;
          this.String(str);
          break;
        default:
          this.Builder.WriteConstant(value);
          break;
      }
    }

    /// <summary>
    /// Encodes literal of type <see cref="T:System.Type" /> (possibly null).
    /// </summary>
    /// <param name="serializedTypeName">The name of the type, or null.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="serializedTypeName" /> is empty.</exception>
    public void SystemType(string? serializedTypeName)
    {
      if (serializedTypeName != null && serializedTypeName.Length == 0)
        Throw.ArgumentEmptyString(nameof (serializedTypeName));
      this.String(serializedTypeName);
    }


    #nullable disable
    private void String(string value) => this.Builder.WriteSerializedString(value);
  }
}
