namespace Datadog.Trace
{
    /// <summary>
    /// ServiceInfo are metadata used to display services in DataDog's UX
    /// </summary>
    public class ServiceInfo
    {
        /// <summary>
        /// Gets or sets the service Name
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the name of the application
        /// </summary>
        public string App { get; set; }

        /// <summary>
        /// Gets or sets the type of the application
        /// </summary>
        public string AppType { get; set; }
    }
}
