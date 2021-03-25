namespace Datadog.Trace.Abstractions
{
    internal interface IMetrics
    {
        /// <summary>
        /// Increments the specified counter.
        /// </summary>
        /// <param name="statName">The name of the metric.</param>
        /// <param name="value">The amount of increment.</param>
        /// <param name="sampleRate">Percentage of metric to be sent.</param>
        /// <param name="tags">Array of tags to be added to the data.</param>
        void Increment(string statName, int value = 1, double sampleRate = 1, string[] tags = null);
    }
}