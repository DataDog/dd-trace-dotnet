namespace Datadog.Trace
{
    /// <summary>
    /// ServiceInfo are metadata used to display services in DataDog's UX
    /// </summary>
    public class ServiceInfo
    {
        /// <summary>
        /// The service Name
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// The name of the application
        /// </summary>
        public string App { get; set; }

        /// <summary>
        /// The type of the application
        /// </summary>
        public string AppType { get; set; }
    }
}
