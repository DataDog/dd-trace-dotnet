using System;
using System.Net.Http;
using System.Threading.Tasks;
using Samples;

const string TransactionHeader = "X-Transaction-Id";
const int RequestCount = 3;

string port = args.Length > 0 && args[0].StartsWith("Port=") ? args[0].Split('=')[1] : "9000";

using var server = WebServer.Start(port, out var url);
server.RequestHandler = ctx =>
{
    ctx.Response.StatusCode = 200;
    ctx.Response.ContentLength64 = 0;
    ctx.Response.Close();
};

Console.WriteLine($"HTTP listener started at {url}");

using var client = new HttpClient();
for (var i = 1; i <= RequestCount; i++)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Add(TransactionHeader, $"txn-{i}");
    await client.SendAsync(request);
    Console.WriteLine($"Sent request {i} with {TransactionHeader}: txn-{i}");
}

Console.WriteLine("Done");
