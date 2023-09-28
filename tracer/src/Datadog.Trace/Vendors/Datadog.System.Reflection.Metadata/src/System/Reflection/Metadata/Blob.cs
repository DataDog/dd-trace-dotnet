// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Blob
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct Blob
  {
    internal readonly byte[] Buffer;
    internal readonly int Start;

    public int Length { get; }

    internal Blob(byte[] buffer, int start, int length)
    {
      this.Buffer = buffer;
      this.Start = start;
      this.Length = length;
    }

    public bool IsDefault => this.Buffer == null;

    public ArraySegment<byte> GetBytes() => new ArraySegment<byte>(this.Buffer, this.Start, this.Length);
  }
}
