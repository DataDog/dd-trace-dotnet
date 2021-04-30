namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.CosmosClientContext for duck typing
    /// </summary>
    public interface ICosmosContextClient
    {
        /// <summary>
        /// Gets the CosmosClient
        /// </summary>
        ICosmosClient Client { get; }
    }
}
