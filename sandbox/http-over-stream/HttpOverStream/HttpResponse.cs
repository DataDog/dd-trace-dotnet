using System.IO;

namespace HttpOverStream
{
    public class HttpResponse : HttpMessage
    {
        public int StatusCode { get; }

        public string ResponseMessage { get; }

        public HttpResponse(int statusCode, string responseMessage, HttpHeaders headers, IHttpContent content)
            : base(headers, content)
        {
            StatusCode = statusCode;
            ResponseMessage = responseMessage;
        }
    }
}
