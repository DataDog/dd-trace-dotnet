using System;
using System.IO;
using System.Text;

namespace HttpOverStream
{
    public class HttpClient
    {
        private const string CrLf = "\r\n";
        private const int bufferSize = 10240;

        public HttpResponse Send(HttpRequest request, Stream requestStream, Stream responseStream)
        {
            // TODO: support async and cancellation
            SendRequest(request, requestStream);
            return ReadResponse(responseStream);
        }

        private static void SendRequest(HttpRequest request, Stream requestStream)
        {
            // optimization opportunity: cache the ascii-encoded bytes of commonly-used headers
            using (var writer = new StreamWriter(requestStream, Encoding.ASCII, bufferSize, leaveOpen: true))
            {
                writer.Write($"{request.Verb} {request.Path} HTTP/1.1{CrLf}");

                writer.Write($"Host: {request.Host}{CrLf}");
                writer.Write($"Accept-Encoding: identity{CrLf}");
                writer.Write($"User-Agent: dd-trace-dotnet/1.20{CrLf}");
                //writer.Write($"Connection: close{CrLf}");
                writer.Write($"Content-Length: {request.Content.Length ?? 0}{CrLf}");

                foreach (var header in request.Headers)
                {
                    writer.Write($"{header.Name}: {header.Value}{CrLf}");
                }

                writer.Write(CrLf);
            }

            request.Content.CopyTo(requestStream);
            requestStream.Flush();
        }

        private static HttpResponse ReadResponse(Stream responseStream)
        {
            var headers = new HttpHeaders();
            int statusCode = 0;
            string responseMessage = null;

            // hack: buffer the entire response so we can seek
            var memoryStream = new MemoryStream();
            responseStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            int streamPosition = 0;

            using (var reader = new StreamReader(memoryStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize, leaveOpen: true))
            {
                // HTTP/1.1 200 OK
                string line = reader.ReadLine();
                streamPosition += reader.CurrentEncoding.GetByteCount(line) + CrLf.Length;

                string statusCodeString = line.Substring(9, 3);
                statusCode = int.Parse(statusCodeString);
                responseMessage = line.Substring(13);

                // read headers
                while (true)
                {
                    line = reader.ReadLine();
                    streamPosition += reader.CurrentEncoding.GetByteCount(line) + CrLf.Length;

                    if (line == "")
                    {
                        // end of headers
                        break;
                    }

                    string[] headerParts = line.Split(':');
                    string name = headerParts[0].Trim();
                    string value = headerParts[1].Trim();
                    headers.Add(name, value);
                }
            }

            int? length = int.TryParse(headers.GetValue("Content-Length"), out int headerValue) ? headerValue : (int?)null;
            return new HttpResponse(statusCode, responseMessage, headers, new StreamContent(responseStream, length));
        }
    }
}
