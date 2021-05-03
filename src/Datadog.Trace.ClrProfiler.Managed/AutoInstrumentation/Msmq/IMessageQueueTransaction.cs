namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq
{
    /// <summary>
    /// message queue transaction
    /// </summary>
    public interface IMessageQueueTransaction
    {
        /// <summary>
        /// Gets status
        /// </summary>
        MessageQueueTransactionStatus Status { get;  }
    }
}
