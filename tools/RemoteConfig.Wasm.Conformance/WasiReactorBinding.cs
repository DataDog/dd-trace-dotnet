using Datadog.Trace.RemoteConfigurationManagement.Shared;
using Wasmtime;

namespace Datadog.Trace.Tools.RemoteConfigConformance;

internal sealed class WasiReactorBinding(Instance instance) : IReactorBinding
{
    private const int AbiVersion = 4;

    private readonly Memory _memory = instance.GetMemory("memory") ?? throw new InvalidOperationException("Missing reactor memory");
    private readonly Func<int> _error = Require(instance.GetFunction<int>("rc_error"), "rc_error");
    private readonly Func<int, int> _address = Require(instance.GetFunction<int, int>("rc_buffer_ptr"), "rc_buffer_ptr");

    internal static Reactor Open(Store store, Linker linker, Module module)
    {
        store.SetWasiConfiguration(new WasiConfiguration());
        var instance = linker.Instantiate(store, module);
        instance.GetAction("_initialize")?.Invoke();
        if (instance.GetFunction<int>("rc_version")?.Invoke() != AbiVersion)
        {
            throw new InvalidOperationException($"Expected WASI ABI v{AbiVersion}");
        }

        return Reactor.OpenBinding(new WasiReactorBinding(instance));
    }

    public int GetError() => _error();

    public Func<int, int> BindUnary(string export) => Require(instance.GetFunction<int, int>(export), export);

    public Func<int, int, int> BindBinary(string export) => Require(instance.GetFunction<int, int, int>(export), export);

    public Func<int, int, int, int> BindTernary(string export) => Require(instance.GetFunction<int, int, int, int>(export), export);

    public Func<int, int, int, int, int> BindQuaternary(string export) => Require(instance.GetFunction<int, int, int, int, int>(export), export);

    public ReadOnlySpan<byte> BorrowRead(int handle, int length) => BorrowWrite(handle, length);

    public Span<byte> BorrowWrite(int handle, int length)
    {
        var address = _address(handle);
        if (address == -1)
        {
            throw new InvalidOperationException("Invalid WASI reactor buffer");
        }

        return _memory.GetSpan(unchecked((uint)address), length);
    }

    public void Dispose()
    {
    }

    private static TDelegate Require<TDelegate>(TDelegate? function, string export)
        where TDelegate : Delegate => function ?? throw new InvalidOperationException($"Invalid ABI export: {export}");
}
