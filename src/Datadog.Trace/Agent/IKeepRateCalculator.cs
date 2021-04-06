namespace Datadog.Trace.Agent
{
    internal interface IKeepRateCalculator
    {
        /// <summary>
        /// Increment the number of kept traces
        /// </summary>
        void IncrementKeeps(int count);

        /// <summary>
        /// Increment the number of dropped traces
        /// </summary>
        void IncrementDrops(int count);

        /// <summary>
        /// Get the current keep rate for traces
        /// </summary>
        double GetKeepRate();

        /// <summary>
        /// Stop updating the buckets. The current Keep rate can continue to be read.
        /// </summary>
        void CancelUpdates();
    }
}
