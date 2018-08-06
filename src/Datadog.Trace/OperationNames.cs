namespace Datadog.Trace
{
    /// <summary>
    /// Contains a set of standard operation names that can be used by integrations.
    /// </summary>
    public static class OperationNames
    {
        /// <summary>
        /// Gets the operation name for an ASP.NET MVC web request.
        /// </summary>
        public static string AspNetMvcRequest => "aspnet_mvc.request";

        /// <summary>
        /// Gets the operation name for an ASP.NET Core MVC web request.
        /// </summary>
        public static string AspNetCoreMvcRequest => "aspnet_core_mvc.request";
    }
}
