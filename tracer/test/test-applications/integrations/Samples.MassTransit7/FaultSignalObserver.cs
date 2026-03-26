using MassTransit;

namespace Samples.MassTransit7;

internal sealed class FaultSignalObserver : IPublishObserver, ISendObserver
{
    readonly IReadOnlyDictionary<Type, string> _signalKeys;

    public FaultSignalObserver(IReadOnlyDictionary<Type, string> signalKeys)
    {
        _signalKeys = signalKeys;
    }

    public Task PrePublish<T>(PublishContext<T> context)
        where T : class
        => Task.CompletedTask;

    public Task PostPublish<T>(PublishContext<T> context)
        where T : class
    {
        TrySetSignal<T>();
        return Task.CompletedTask;
    }

    public Task PublishFault<T>(PublishContext<T> context, Exception exception)
        where T : class
        => Task.CompletedTask;

    public Task PreSend<T>(SendContext<T> context)
        where T : class
        => Task.CompletedTask;

    public Task PostSend<T>(SendContext<T> context)
        where T : class
    {
        TrySetSignal<T>();
        return Task.CompletedTask;
    }

    public Task SendFault<T>(SendContext<T> context, Exception exception)
        where T : class
        => Task.CompletedTask;

    void TrySetSignal<T>()
        where T : class
    {
        if (_signalKeys.TryGetValue(typeof(T), out var key))
        {
            TestSignal.Set(key);
        }
    }
}
