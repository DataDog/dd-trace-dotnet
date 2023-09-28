// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.ByteArrayMemoryBlock
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
  /// <summary>
  /// Represents a memory block backed by an array of bytes.
  /// </summary>
  internal sealed class ByteArrayMemoryBlock : AbstractMemoryBlock
  {

    #nullable disable
    private ByteArrayMemoryProvider _provider;
    private readonly int _start;
    private readonly int _size;


    #nullable enable
    internal ByteArrayMemoryBlock(ByteArrayMemoryProvider provider, int start, int size)
    {
      this._provider = provider;
      this._size = size;
      this._start = start;
    }

    public override void Dispose() => this._provider = (ByteArrayMemoryProvider) null;

    public override unsafe byte* Pointer => this._provider.Pointer + this._start;

    public override int Size => this._size;

    public override ImmutableArray<byte> GetContentUnchecked(int start, int length) => ImmutableArray.Create<byte>(this._provider.Array, this._start + start, length);
  }
}
