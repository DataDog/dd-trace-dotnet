﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.NamedArgumentsEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct NamedArgumentsEncoder
  {
    public BlobBuilder Builder { get; }

    public NamedArgumentsEncoder(BlobBuilder builder) => this.Builder = builder;

    /// <summary>
    /// Encodes a named argument (field or property).
    /// Returns a triplet of encoders that must be used in the order they appear in the parameter list.
    /// </summary>
    /// <param name="isField">True to encode a field, false to encode a property.</param>
    /// <param name="type">Use first, to encode the type of the argument.</param>
    /// <param name="name">Use second, to encode the name of the field or property.</param>
    /// <param name="literal">Use third, to encode the literal value of the argument.</param>
    public void AddArgument(
      bool isField,
      out NamedArgumentTypeEncoder type,
      out NameEncoder name,
      out LiteralEncoder literal)
    {
      this.Builder.WriteByte(isField ? (byte) 83 : (byte) 84);
      type = new NamedArgumentTypeEncoder(this.Builder);
      name = new NameEncoder(this.Builder);
      literal = new LiteralEncoder(this.Builder);
    }

    /// <summary>Encodes a named argument (field or property).</summary>
    /// <param name="isField">True to encode a field, false to encode a property.</param>
    /// <param name="type">Called first, to encode the type of the argument.</param>
    /// <param name="name">Called second, to encode the name of the field or property.</param>
    /// <param name="literal">Called third, to encode the literal value of the argument.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="type" />, <paramref name="name" /> or <paramref name="literal" /> is null.</exception>
    public void AddArgument(
      bool isField,
      Action<NamedArgumentTypeEncoder> type,
      Action<NameEncoder> name,
      Action<LiteralEncoder> literal)
    {
      if (type == null)
        Throw.ArgumentNull(nameof (type));
      if (name == null)
        Throw.ArgumentNull(nameof (name));
      if (literal == null)
        Throw.ArgumentNull(nameof (literal));
      NamedArgumentTypeEncoder type1;
      NameEncoder name1;
      LiteralEncoder literal1;
      this.AddArgument(isField, out type1, out name1, out literal1);
      type(type1);
      name(name1);
      literal(literal1);
    }
  }
}
