// Decompiled with JetBrains decompiler
// Type: System.Buffers.MemoryManager`1
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.Runtime.CompilerServices;

namespace Datadog.System.Buffers
{
    public abstract class MemoryManager<T> : IMemoryOwner<T>, IDisposable, IPinnable
  {
    public virtual System.Memory<T> Memory => new System.Memory<T>(this, this.GetSpan().Length);

    public abstract Span<T> GetSpan();

    public abstract MemoryHandle Pin(int elementIndex = 0);

    public abstract void Unpin();

    [MethodImpl((MethodImplOptions) 256)]
    protected System.Memory<T> CreateMemory(int length) => new System.Memory<T>(this, length);

    [MethodImpl((MethodImplOptions) 256)]
    protected System.Memory<T> CreateMemory(int start, int length) => new System.Memory<T>(this, start, length);

    protected internal virtual bool TryGetArray(out ArraySegment<T> segment)
    {
      segment = new ArraySegment<T>();
      return false;
    }

    void IDisposable.Dispose()
    {
      this.Dispose(true);
      GC.SuppressFinalize((object) this);
    }

    protected abstract void Dispose(bool disposing);
  }
}
