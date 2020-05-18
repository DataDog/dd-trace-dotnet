namespace Datadog.Trace.Vendors.System.Net.Http
{
	partial class HttpClientHandler : HttpMessageHandler
	{
		static IMonoHttpClientHandler CreateDefaultHandler () => new SocketsHttpHandler ();
	}
}
