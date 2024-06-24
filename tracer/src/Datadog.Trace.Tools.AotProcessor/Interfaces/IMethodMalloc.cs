using Newtonsoft.Json.Linq;

namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface IMethodMalloc : IUnknown
{
    public static readonly new Guid Guid = new("A0EFB28B-6EE2-4d7b-B983-A75EF7BEEDB8");

    IntPtr Alloc(uint cb);
}
