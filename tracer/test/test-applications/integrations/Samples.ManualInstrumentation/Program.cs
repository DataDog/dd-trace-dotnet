using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Samples;

var shouldBeAttached = Environment.GetEnvironmentVariable("AUTO_INSTRUMENT_ENABLED") == "1";
var runInstrumentationChecks = shouldBeAttached;

var isManualOnly = (bool)typeof(Tracer)
                          .Assembly
                          .GetType("Datadog.Trace.ClrProfiler.Instrumentation", throwOnError: true)
                           !.GetMethod("IsManualInstrumentationOnly")
                           !.Invoke(null, null)!;
Expect(isManualOnly != shouldBeAttached);
Expect(SampleHelpers.IsProfilerAttached() == shouldBeAttached);

var count = 0;
var port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? "9000";
Console.WriteLine($"Port {port}");
var server = WebServer.Start(port, out var url);
var client = new HttpClient();
server.RequestHandler = HandleHttpRequests;

// Manually enable debug logs
GlobalSettings.SetDebugEnabled(true);
LogCurrentSettings(Tracer.Instance, "Initial");

// verify instrumentation
ThrowIf(string.IsNullOrEmpty(Tracer.Instance.DefaultServiceName));

// Manual + automatic before reconfiguration
var firstOperationName = $"Manual-{++count}.Initial";
using (var scope = Tracer.Instance.StartActive(firstOperationName))
{
    // All these should be satisfied when we're instrumented
    Expect(Tracer.Instance.ActiveScope is not null);
    Expect(scope.Span.OperationName == firstOperationName);
    Expect(scope.Span.SpanId != 0);
    Expect(scope.Span.TraceId != 0);
    scope.Span.SetTag("Temp", "TempTest");
    Expect(scope.Span.GetTag("Temp") == "TempTest");
    scope.Span.SetTag("Temp", null);

    await SendHttpRequest("Initial");
}
await Tracer.Instance.ForceFlushAsync();

// Reconfigure the tracer
var settings = TracerSettings.FromDefaultSources();
settings.ServiceName = "updated-name";
settings.Environment = "updated-env";
settings.GlobalTags = new Dictionary<string, string> { { "Updated-key", "Updated Value" } };
Tracer.Configure(settings);
LogCurrentSettings(Tracer.Instance, "Reconfigured");

// Manual + automatic
using (Tracer.Instance.StartActive($"Manual-{++count}.Reconfigured"))
{
    await SendHttpRequest("Reconfigured");
}
await Tracer.Instance.ForceFlushAsync();

// reconfigure with Http disabled
settings = TracerSettings.FromDefaultSources();
// net core
var httpIntegration = settings.Integrations["HttpMessageHandler"];
httpIntegration.Enabled = false;
httpIntegration.AnalyticsEnabled = false; // just setting them because why not
httpIntegration.AnalyticsSampleRate = 1.0;
// net FX
httpIntegration = settings.Integrations["WebRequest"];
httpIntegration.Enabled = false;
httpIntegration.AnalyticsEnabled = false; // just setting them because why not
httpIntegration.AnalyticsSampleRate = 1.0;
Tracer.Configure(settings);
LogCurrentSettings(Tracer.Instance, "HttpDisabled");

// send a trace with it disabled
using (Tracer.Instance.StartActive($"Manual-{++count}.HttpDisabled"))
{
    await SendHttpRequest("HttpDisabled");
}
await Tracer.Instance.ForceFlushAsync();

// go back to the defaults
Tracer.Configure(TracerSettings.FromDefaultSources());
LogCurrentSettings(Tracer.Instance, "DefaultsReinstated");
using (Tracer.Instance.StartActive($"Manual-{++count}.DefaultsReinstated"))
{
    await SendHttpRequest("DefaultsReinstated");
}
await Tracer.Instance.ForceFlushAsync();

// nested manual + extensions
using (var s1 = Tracer.Instance.StartActive($"Manual-{++count}.Ext.Outer"))
{
    s1.Span.SetTraceSamplingPriority(SamplingPriority.UserKeep);

    using var s2 = Tracer.Instance.StartActive($"Manual-{count}.Ext.Inner");
    s2.Span.SetException(new CustomException());
    s2.Span.SetTag("Custom", "Some-Value");
    s2.Span.SetTag("Some-Number", 123);
    s2.Span.SetUser(
        new UserDetails("my-id")
        {
            Email = "test@example.com",
            Name = "Bits",
            Role = "Mascot",
            Scope = "test-scope",
            PropagateId = true,
            SessionId = "abc123"
        });

    await SendHttpRequest("Ext");
}

// nested manual + eventSDK
using (Tracer.Instance.StartActive($"Manual-{++count}.EventSdk.Custom.Outer"))
{
    using var s2 = Tracer.Instance.StartActive($"Manual-{count}.EventSdk.Custom.Inner");
    EventTrackingSdk.TrackCustomEvent("custom-event");
    EventTrackingSdk.TrackCustomEvent("custom-event-meta", new Dictionary<string, string> { { "key-1", "val-1" }, { "key-2", "val-2" }, });
    await SendHttpRequest("Ext");
}

using (Tracer.Instance.StartActive($"Manual-{++count}.EventSdk.Success.Outer"))
{
    using var s2 = Tracer.Instance.StartActive($"Manual-{count}.EventSdk.Success.Inner");
    EventTrackingSdk.TrackUserLoginSuccessEvent("my-id");
    EventTrackingSdk.TrackUserLoginSuccessEvent("my-id", new Dictionary<string, string> { { "key-1", "val-1" }, { "key-2", "val-2" }, });
    await SendHttpRequest("Ext");
}

using (Tracer.Instance.StartActive($"Manual-{++count}.EventSdk.Failure.Outer"))
{
    using var s2 = Tracer.Instance.StartActive($"Manual-{count}.EventSdk.Failure.Inner");
    EventTrackingSdk.TrackUserLoginFailureEvent("my-id", true);
    EventTrackingSdk.TrackUserLoginFailureEvent("my-id", true, new Dictionary<string, string> { { "key-1", "val-1" }, { "key-2", "val-2" }, });
    await SendHttpRequest("Ext");
}

// Custom context
var parent = new SpanContext(traceId: 1234567, 7654321, SamplingPriority.AutoKeep, "manual-parent");
var createSpan = new SpanCreationSettings
{
    FinishOnClose = false,
    StartTime = DateTimeOffset.Now.AddHours(-1),
    Parent = parent,
};
using (var s1 = Tracer.Instance.StartActive($"Manual-{++count}.CustomContext", createSpan))
{
    s1.Span.ServiceName = Tracer.Instance.DefaultServiceName;
    await SendHttpRequest("CustomContext");
}

// Manually disable debug logs
GlobalSettings.SetDebugEnabled(false);

// Force flush
await Tracer.Instance.ForceFlushAsync();
    
// Force process to end, otherwise the background listener thread lives forever in .NET Core.
// Apparently listener.GetContext() doesn't throw an exception if listener.Stop() is called,
// like it does in .NET Framework.
server.Dispose();
Environment.Exit(0);
return;

async Task SendHttpRequest(string name)
{
    var q = $"{count}.{name}";
    using var scope = Tracer.Instance.StartActive($"Manual-{q}.HttpClient");
    await client.GetAsync(url + $"?q={q}");
    Console.WriteLine("Received response for client.GetAsync(String)");
}

void HandleHttpRequests(HttpListenerContext context)
{
    try
    {
        var query = context.Request.QueryString["q"];
        using var scope = Tracer.Instance.StartActive($"Manual-{query}.HttpListener");
        Console.WriteLine("[HttpListener] received request");

        // read request content and headers
        using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
        {
            string requestContent = reader.ReadToEnd();
            Console.WriteLine($"[HttpListener] request content: {requestContent}");
        }

        // write response content
        scope.Span.SetTag("content", "PONG");
        var responseBytes = Encoding.UTF8.GetBytes("PONG");
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentLength64 = responseBytes.Length;
        context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
        // we must close the response
        context.Response.Close();
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
        context.Response.Close();
        throw;
    }
}

static void LogCurrentSettings(Tracer tracer, string step)
{
    var settings = tracer.Settings;
    Console.WriteLine($"Current tracer settings for {step}: ");

    WriteLog(settings.Environment);
    WriteLog(settings.ServiceName);
    WriteLog(settings.ServiceVersion);
    var globalTags = string.Join(". ", settings.GlobalTags.Select(x => $"{x.Key}:{x.Value}"));
    WriteLog(globalTags);

    static void WriteLog(object argument, [CallerArgumentExpression(nameof(argument))] string paramName = null)
    {
        Console.WriteLine($"  {paramName}: {argument}");
    }
}

void Expect(bool condition, [CallerArgumentExpression(nameof(condition))] string description = null)
{
    if (runInstrumentationChecks && !condition)
    {
        throw new InstrumentationErrorException(description, expected: true);
    }
}

void ThrowIf(bool condition, [CallerArgumentExpression(nameof(condition))] string description = null)
{
    if (runInstrumentationChecks && condition)
    {
        throw new InstrumentationErrorException(description, expected: false);
    }
}

class CustomException : Exception
{
}

class InstrumentationErrorException(string condition, bool expected)
    : Exception($"Instrumentation of manual API error: {condition} should be {expected} when automatic instrumentation is running correctly, but was {!expected}")
{
}
