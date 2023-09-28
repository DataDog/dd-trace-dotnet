// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.ByteArrayMemoryProvider
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.IO;
using System.Threading;
using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
    internal sealed class ByteArrayMemoryProvider : MemoryBlockProvider
  {

    #nullable disable
    private readonly ImmutableArray<byte> _array;
    private PinnedObject _pinned;


    #nullable enable
    public ByteArrayMemoryProvider(ImmutableArray<byte> array) => this._array = array;

    protected override void Dispose(bool disposing) => Interlocked.Exchange<PinnedObject>(ref this._pinned, (PinnedObject) null)?.Dispose();

    public override int Size => this._array.Length;

    public ImmutableArray<byte> Array => this._array;

    protected override AbstractMemoryBlock GetMemoryBlockImpl(int start, int size) => (AbstractMemoryBlock) new ByteArrayMemoryBlock(this, start, size);

    public override Stream GetStream(out StreamConstraints constraints)
    {
      constraints = new StreamConstraints((object) null, 0L, this.Size);
      return (Stream) new ImmutableMemoryStream(this._array);
    }

    internal unsafe byte* Pointer
    {
      get
      {
        if (this._pinned == null)
        {
          PinnedObject pinnedObject = new PinnedObject((object) ImmutableByteArrayInterop.DangerousGetUnderlyingArray(this._array));
          if (Interlocked.CompareExchange<PinnedObject>(ref this._pinned, pinnedObject, (PinnedObject) null) != null)
            pinnedObject.Dispose();
        }
        return this._pinned.Pointer;
      }
    }
  }
}
