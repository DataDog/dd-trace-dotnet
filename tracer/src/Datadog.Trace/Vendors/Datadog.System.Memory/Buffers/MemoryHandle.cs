// Decompiled with JetBrains decompiler
// Type: System.Buffers.MemoryHandle
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.Runtime.InteropServices;

namespace Datadog.System.Buffers
{
    public struct MemoryHandle : IDisposable
  {
    private unsafe void* _pointer;
    private GCHandle _handle;
    private IPinnable _pinnable;

    [CLSCompliant(false)]
    public unsafe MemoryHandle(void* pointer, GCHandle handle = default (GCHandle), IPinnable pinnable = null)
    {
      this._pointer = pointer;
      this._handle = handle;
      this._pinnable = pinnable;
    }

    [CLSCompliant(false)]
    public unsafe void* Pointer => this._pointer;

    public unsafe void Dispose()
    {
      if (this._handle.IsAllocated)
        this._handle.Free();
      if (this._pinnable != null)
      {
        this._pinnable.Unpin();
        this._pinnable = (IPinnable) null;
      }
      this._pointer = (void*) null;
    }
  }
}
