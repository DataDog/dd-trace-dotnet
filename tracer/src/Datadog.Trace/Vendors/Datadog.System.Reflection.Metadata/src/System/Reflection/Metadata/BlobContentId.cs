﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.BlobContentId
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections.Generic;
using Datadog.System.Collections.Immutable;
using Datadog.System.Diagnostics.CodeAnalysis;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    public readonly struct BlobContentId : IEquatable<BlobContentId>
  {
    private const int Size = 20;

    public Guid Guid { get; }

    public uint Stamp { get; }

    public BlobContentId(Guid guid, uint stamp)
    {
      this.Guid = guid;
      this.Stamp = stamp;
    }

    public BlobContentId(ImmutableArray<byte> id)
      : this(ImmutableByteArrayInterop.DangerousGetUnderlyingArray(id))
    {
    }

    public unsafe BlobContentId(byte[] id)
    {
      if (id == null)
        Throw.ArgumentNull(nameof (id));
      if (id.Length != 20)
        throw new ArgumentException(SR.Format(SR.UnexpectedArrayLength, (object) 20), nameof (id));
      fixed (byte* buffer = &id[0])
      {
        BlobReader blobReader = new BlobReader(buffer, id.Length);
        this.Guid = blobReader.ReadGuid();
        this.Stamp = blobReader.ReadUInt32();
      }
    }

    public bool IsDefault => this.Guid == new Guid() && this.Stamp == 0U;

    public static BlobContentId FromHash(ImmutableArray<byte> hashCode) => BlobContentId.FromHash(ImmutableByteArrayInterop.DangerousGetUnderlyingArray(hashCode));

    public static BlobContentId FromHash(byte[] hashCode)
    {
      if (hashCode == null)
        Throw.ArgumentNull(nameof (hashCode));
      if (hashCode.Length < 20)
        throw new ArgumentException(SR.Format(SR.HashTooShort, (object) 20), nameof (hashCode));
      uint a = (uint) ((int) hashCode[3] << 24 | (int) hashCode[2] << 16 | (int) hashCode[1] << 8) | (uint) hashCode[0];
      ushort b = (ushort) ((uint) hashCode[5] << 8 | (uint) hashCode[4]);
      ushort num1 = (ushort) ((uint) hashCode[7] << 8 | (uint) hashCode[6]);
      byte num2 = hashCode[8];
      byte e = hashCode[9];
      byte f = hashCode[10];
      byte g = hashCode[11];
      byte h = hashCode[12];
      byte i = hashCode[13];
      byte j = hashCode[14];
      byte k = hashCode[15];
      ushort c = (ushort) ((int) num1 & 4095 | 16384);
      byte d = (byte) ((int) num2 & 63 | 128);
      return new BlobContentId(new Guid((int) a, (short) b, (short) c, d, e, f, g, h, i, j, k), (uint) (int.MinValue | (int) hashCode[19] << 24 | (int) hashCode[18] << 16 | (int) hashCode[17] << 8 | (int) hashCode[16]));
    }

    public static Func<IEnumerable<Blob>, BlobContentId> GetTimeBasedProvider()
    {
      uint timestamp = (uint) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
      return (Func<IEnumerable<Blob>, BlobContentId>) (content => new BlobContentId(Guid.NewGuid(), timestamp));
    }

    public bool Equals(BlobContentId other) => this.Guid == other.Guid && (int) this.Stamp == (int) other.Stamp;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is BlobContentId other && this.Equals(other);

    public override int GetHashCode() => Hash.Combine(this.Stamp, this.Guid.GetHashCode());

    public static bool operator ==(BlobContentId left, BlobContentId right) => left.Equals(right);

    public static bool operator !=(BlobContentId left, BlobContentId right) => !left.Equals(right);
  }
}
