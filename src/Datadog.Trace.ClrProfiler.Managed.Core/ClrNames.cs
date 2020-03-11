namespace Datadog.Trace.ClrProfiler
{
    internal static class ClrNames
    {
        public const string Ignore = "_";

        public const string Void = "System.Void";
        public const string Object = "System.Object";
        public const string Bool = "System.Boolean";
        public const string String = "System.String";

        public const string SByte = "System.SByte";
        public const string Byte = "System.Byte";

        public const string Int16 = "System.Int16";
        public const string Int32 = "System.Int32";
        public const string Int64 = "System.Int64";

        public const string UInt16 = "System.UInt16";
        public const string UInt32 = "System.UInt32";
        public const string UInt64 = "System.UInt64";

        public const string Task = "System.Threading.Tasks.Task";
        public const string CancellationToken = "System.Threading.CancellationToken";

        // ReSharper disable once InconsistentNaming
        public const string IAsyncResult = "System.IAsyncResult";
        public const string AsyncCallback = "System.AsyncCallback";

        public const string HttpRequestMessage = "System.Net.Http.HttpRequestMessage";
        public const string HttpResponseMessageTask = "System.Threading.Tasks.Task`1<System.Net.Http.HttpResponseMessage>";

        public const string GenericTask = "System.Threading.Tasks.Task`1";
    }
}
