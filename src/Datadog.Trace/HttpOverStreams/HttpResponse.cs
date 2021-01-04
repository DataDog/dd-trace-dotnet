namespace Datadog.Trace.HttpOverStreams
{
    internal class HttpResponse : HttpMessage
    {
        public HttpResponse(int statusCode, string responseMessage, HttpHeaders headers, IHttpContent content)
            : base(headers, content)
        {
            StatusCode = statusCode;
            ResponseMessage = responseMessage;
        }

        public int StatusCode { get; }

        public string ResponseMessage { get; }
    }
}
