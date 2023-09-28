// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.PinnedObject
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
    internal sealed class PinnedObject : CriticalDisposableObject
  {
    private GCHandle _handle;
    private int _isValid;

    public PinnedObject(object obj)
    {
      RuntimeHelpers.PrepareConstrainedRegions();
      try
      {
      }
      finally
      {
        this._handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
        this._isValid = 1;
      }
    }

    protected override void Release()
    {
      RuntimeHelpers.PrepareConstrainedRegions();
      try
      {
      }
      finally
      {
        if (Interlocked.Exchange(ref this._isValid, 0) != 0)
          this._handle.Free();
      }
    }

    public unsafe byte* Pointer => (byte*) (void*) this._handle.AddrOfPinnedObject();
  }
}
