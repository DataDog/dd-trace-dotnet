﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.LiteralEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct LiteralEncoder
  {
    public BlobBuilder Builder { get; }

    public LiteralEncoder(BlobBuilder builder) => this.Builder = builder;

    public VectorEncoder Vector() => new VectorEncoder(this.Builder);

    /// <summary>
    /// Encodes the type and the items of a vector literal.
    /// Returns a pair of encoders that must be used in the order they appear in the parameter list.
    /// </summary>
    /// <param name="arrayType">Use first, to encode the type of the vector.</param>
    /// <param name="vector">Use second, to encode the items of the vector.</param>
    public void TaggedVector(
      out CustomAttributeArrayTypeEncoder arrayType,
      out VectorEncoder vector)
    {
      arrayType = new CustomAttributeArrayTypeEncoder(this.Builder);
      vector = new VectorEncoder(this.Builder);
    }

    /// <summary>Encodes the type and the items of a vector literal.</summary>
    /// <param name="arrayType">Called first, to encode the type of the vector.</param>
    /// <param name="vector">Called second, to encode the items of the vector.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="arrayType" /> or <paramref name="vector" /> is null.</exception>
    public void TaggedVector(
      Action<CustomAttributeArrayTypeEncoder> arrayType,
      Action<VectorEncoder> vector)
    {
      if (arrayType == null)
        Throw.ArgumentNull(nameof (arrayType));
      if (vector == null)
        Throw.ArgumentNull(nameof (vector));
      CustomAttributeArrayTypeEncoder arrayType1;
      VectorEncoder vector1;
      this.TaggedVector(out arrayType1, out vector1);
      arrayType(arrayType1);
      vector(vector1);
    }

    /// <summary>Encodes a scalar literal.</summary>
    /// <returns>Encoder of the literal value.</returns>
    public ScalarEncoder Scalar() => new ScalarEncoder(this.Builder);

    /// <summary>
    /// Encodes the type and the value of a literal.
    /// Returns a pair of encoders that must be used in the order they appear in the parameter list.
    /// </summary>
    /// <param name="type">Called first, to encode the type of the literal.</param>
    /// <param name="scalar">Called second, to encode the value of the literal.</param>
    public void TaggedScalar(out CustomAttributeElementTypeEncoder type, out ScalarEncoder scalar)
    {
      type = new CustomAttributeElementTypeEncoder(this.Builder);
      scalar = new ScalarEncoder(this.Builder);
    }

    /// <summary>Encodes the type and the value of a literal.</summary>
    /// <param name="type">Called first, to encode the type of the literal.</param>
    /// <param name="scalar">Called second, to encode the value of the literal.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="type" /> or <paramref name="scalar" /> is null.</exception>
    public void TaggedScalar(
      Action<CustomAttributeElementTypeEncoder> type,
      Action<ScalarEncoder> scalar)
    {
      if (type == null)
        Throw.ArgumentNull(nameof (type));
      if (scalar == null)
        Throw.ArgumentNull(nameof (scalar));
      CustomAttributeElementTypeEncoder type1;
      ScalarEncoder scalar1;
      this.TaggedScalar(out type1, out scalar1);
      type(type1);
      scalar(scalar1);
    }
  }
}
