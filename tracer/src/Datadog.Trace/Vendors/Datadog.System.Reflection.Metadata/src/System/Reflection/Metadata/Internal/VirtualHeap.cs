// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.VirtualHeap
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
    internal sealed class VirtualHeap : CriticalDisposableObject
  {

    #nullable disable
    private Dictionary<uint, VirtualHeap.PinnedBlob> _blobs;

    private VirtualHeap() => this._blobs = new Dictionary<uint, VirtualHeap.PinnedBlob>();

    protected override void Release()
    {
      RuntimeHelpers.PrepareConstrainedRegions();
      try
      {
      }
      finally
      {
        Dictionary<uint, VirtualHeap.PinnedBlob> dictionary = Interlocked.Exchange<Dictionary<uint, VirtualHeap.PinnedBlob>>(ref this._blobs, (Dictionary<uint, VirtualHeap.PinnedBlob>) null);
        if (dictionary != null)
        {
          foreach (KeyValuePair<uint, VirtualHeap.PinnedBlob> keyValuePair in dictionary)
            keyValuePair.Value.Handle.Free();
        }
      }
    }

    private Dictionary<uint, VirtualHeap.PinnedBlob> GetBlobs() => this._blobs ?? throw new ObjectDisposedException(nameof (VirtualHeap));


    #nullable enable
    public bool TryGetMemoryBlock(uint rawHandle, out MemoryBlock block)
    {
      VirtualHeap.PinnedBlob pinnedBlob;
      if (!this.GetBlobs().TryGetValue(rawHandle, out pinnedBlob))
      {
        block = new MemoryBlock();
        return false;
      }
      block = pinnedBlob.GetMemoryBlock();
      return true;
    }

    internal MemoryBlock AddBlob(uint rawHandle, byte[] value)
    {
      Dictionary<uint, VirtualHeap.PinnedBlob> blobs = this.GetBlobs();
      RuntimeHelpers.PrepareConstrainedRegions();
      MemoryBlock memoryBlock;
      try
      {
      }
      finally
      {
        VirtualHeap.PinnedBlob pinnedBlob = new VirtualHeap.PinnedBlob(GCHandle.Alloc((object) value, GCHandleType.Pinned), value.Length);
        blobs.Add(rawHandle, pinnedBlob);
        memoryBlock = pinnedBlob.GetMemoryBlock();
      }
      return memoryBlock;
    }

    internal static VirtualHeap GetOrCreateVirtualHeap(ref VirtualHeap? lazyHeap)
    {
      if (lazyHeap == null)
        Interlocked.CompareExchange<VirtualHeap>(ref lazyHeap, new VirtualHeap(), (VirtualHeap) null);
      return lazyHeap;
    }


    #nullable disable
    private struct PinnedBlob
    {
      public GCHandle Handle;
      public readonly int Length;

      public PinnedBlob(GCHandle handle, int length)
      {
        this.Handle = handle;
        this.Length = length;
      }

      public unsafe MemoryBlock GetMemoryBlock() => new MemoryBlock((byte*) (void*) this.Handle.AddrOfPinnedObject(), this.Length);
    }
  }
}
