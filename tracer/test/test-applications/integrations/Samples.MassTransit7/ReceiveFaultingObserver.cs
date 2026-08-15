using MassTransit;
using Samples.MassTransit;

namespace Samples.MassTransit7;

/// <summary>
/// Signals when MassTransit reports the receive-level fault produced by the malformed message.
/// </summary>
internal sealed class ReceiveFaultingObserver : IReceiveObserver
{
    private readonly string _signalKey;

    public ReceiveFaultingObserver(string signalKey) => _signalKey = signalKey;

    public Task PreReceive(ReceiveContext context) => Task.CompletedTask;

    public Task PostReceive(ReceiveContext context) => Task.CompletedTask;

    public Task PostConsume<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType)
        where T : class => Task.CompletedTask;

    public Task ConsumeFault<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType, Exception exception)
        where T : class => Task.CompletedTask;

    public Task ReceiveFault(ReceiveContext context, Exception exception)
    {
        TestSignal.Set(_signalKey);
        return Task.CompletedTask;
    }
}
