using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.DataStreams.ManualAPI;

public class Program
{
    private static ConcurrentQueue<string> _queue = new();

    private static async Task Main(string[] args)
    {
        var writing = Send("my message");
        var reading = Receive();

        // wait up to 20 second for threads
        await Task.WhenAny(Task.WhenAll(reading, writing), Task.Delay(TimeSpan.FromSeconds(value: 20)));
    }

    private static async Task Send(string message)
    {
        using var scope = Tracer.Instance.StartActive("Samples.DataStreams.ManualAPI.Send");

        await Task.Delay(millisecondsDelay: 100);

        Console.WriteLine("Sending one message to the queue...");
        var sb = new StringBuilder();
        var injector = new SpanContextInjector();
        injector.InjectIncludingDsm(sb, (b, k, v) => b.Append($"{k}:{v};"), scope.Span.Context, "ConcurrentQueue", nameof(_queue));
        sb.Append(message);
        _queue.Enqueue(sb.ToString());
        Console.WriteLine("message sent");
    }

    private static async Task<string> Receive()
    {
        Console.WriteLine("Receiving one message from the queue...");
        string result;
        while (!_queue.TryDequeue(out result))
        {
            await Task.Delay(millisecondsDelay: 100);
            Console.WriteLine("Retrying to receive");
        }

        var headers = Parse(result, out var content);
        Console.WriteLine($"Parsed {headers.Count} headers");
        var extractor = new SpanContextExtractor();
        var extractedContext = extractor.ExtractIncludingDsm(
            headers,
            // complicated getter because we need to return an empty array if no result
            (d, k) =>
            {
                if (d.TryGetValue(k, out var val))
                {
                    return [val];
                }

                return [];
            },
            "ConcurrentQueue",
            nameof(_queue));
        using var scope = Tracer.Instance.StartActive("Samples.DataStreams.ManualAPI.Receive", new SpanCreationSettings { Parent = extractedContext });

        Console.WriteLine("Done");
        return content;
    }

    /// <returns>headers and message</returns>
    private static Dictionary<string, string> Parse(string raw, out string msg)
    {
        var content = raw.Split(separator: ';');
        var headers = content
                     .Take(content.Length - 1) // SkipLast() is unavailable in older versions that we are testing on
                     .Select(s => s.Split(separator: ':'))
                     .ToDictionary(a => a[0], a => a[1]);
        msg = content.Last();
        return headers;
    }
}
