namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq
{
    /// <summary>
    /// IMessageQueueTransactionStatus
    /// </summary>
    public enum MessageQueueTransactionStatus
    {
        /// <summary>
        /// Aborted
        /// </summary>
        Aborted = 0,

        /// <summary>
        ///  Committed
        /// </summary>
        Committed = 1,

        /// <summary>
        ///  Initialized
        /// </summary>
        Initialized = 2,

        /// <summary>
        ///  Pending
        /// </summary>
        Pending
    }
}
