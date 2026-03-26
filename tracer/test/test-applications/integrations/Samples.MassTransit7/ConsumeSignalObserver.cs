using MassTransit;

namespace Samples.MassTransit7;

internal sealed class ConsumeSignalObserver : IConsumeObserver
{
    readonly IReadOnlyDictionary<Type, string> _signalKeys;

    public ConsumeSignalObserver(IReadOnlyDictionary<Type, string> signalKeys)
    {
        _signalKeys = signalKeys;
    }

    public Task PreConsume<T>(ConsumeContext<T> context)
        where T : class
        => Task.CompletedTask;

    public Task PostConsume<T>(ConsumeContext<T> context)
        where T : class
        => Task.CompletedTask;

    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception)
        where T : class
    {
        if (_signalKeys.TryGetValue(typeof(T), out var key))
        {
            TestSignal.Set(key);
        }

        return Task.CompletedTask;
    }
}
