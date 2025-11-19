using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Samples;

internal class Program
{
    private static readonly Tracer _initialTracer;

    static Program()
    {
        // Ensure we initialize the tracer before any other code
        // to test that the automatic startup hook handles this correctly
        _initialTracer = Tracer.Instance;
    }

    public static async Task Main(string[] args)
    {
        await Task.Yield();

        // Do this before anything else to hit ready-to-run issues
        using (_initialTracer.StartActive("initial"))
        {
        }

        // Moving this to a separate method to hit the r2r issue
        await OtherStuff();

        async Task OtherStuff()
        {
            using var mutex = new ManualResetEventSlim();

            var shouldBeAttached = Environment.GetEnvironmentVariable("AUTO_INSTRUMENT_ENABLED") == "1";
            var runInstrumentationChecks = shouldBeAttached;

            var isManualOnly = (bool)typeof(Tracer)
                                    .Assembly
                                    .GetType("Datadog.Trace.ClrProfiler.Instrumentation", throwOnError: true)
                                     !.GetMethod("IsManualInstrumentationOnly")
                                     !.Invoke(null, null)!;

            // It's... weird... but reflection doesn't work with the rewriting in r2r for some reason...
            var hasCorrectValueAfterRewrite = Environment.GetEnvironmentVariable("READY2RUN_ENABLED") != "1";
            if (hasCorrectValueAfterRewrite)
            {
                Expect(isManualOnly != shouldBeAttached);
            }

            Expect(SampleHelpers.IsProfilerAttached() == shouldBeAttached);

            var count = 0;
            var port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? "9000";
            Console.WriteLine($"Port {port}");
            var server = WebServer.Start(port, out var url);
            var client = new HttpClient();
            server.RequestHandler = HandleHttpRequests;

            // Manually enable debug logs
            GlobalSettings.SetDebugEnabled(true);
            LogAndAssertCurrentSettings(_initialTracer, "Initial");

            // verify instrumentation
            ThrowIf(string.IsNullOrEmpty(_initialTracer.DefaultServiceName));

            // baggage works even without an active span
            var baggage = Baggage.Current;
            baggage["key1"] = "value1";
            Expect(baggage.TryGetValue("key1", out var baggageValue1) && baggageValue1 == "value1");

            // Manual + automatic before reconfiguration
            var firstOperationName = $"Manual-{++count}.Initial";
            using (var scope = _initialTracer.StartActive(firstOperationName))
            {
                // All these should be satisfied when we're instrumented
                Expect(_initialTracer.ActiveScope is not null);
                Expect(scope.Span.OperationName == firstOperationName);
                Expect(scope.Span.SpanId != 0);
                Expect(scope.Span.TraceId != 0);
                scope.Span.SetTag("Temp", "TempTest");
                Expect(scope.Span.GetTag("Temp") == "TempTest");
                scope.Span.SetTag("Temp", null);

                // baggage keeps working with an active span
                baggage["key2"] = "value2";
                Expect(baggage.TryGetValue("key2", out var baggageValue2) && baggageValue2 == "value2");

                var responseMessage = await SendHttpRequest("Initial");
                var requestMessage = responseMessage.RequestMessage!;

                // verify baggage in the request headers
                Expect(
                    requestMessage.Headers.TryGetValues("baggage", out var baggageValues) &&
                    baggageValues.FirstOrDefault() == "key1=value1,key2=value2");
            }

            await _initialTracer.ForceFlushAsync();

            // Reconfigure the tracer
            var settings = TracerSettings.FromDefaultSources();
            settings.ServiceName = "updated-name";
            settings.Environment = "updated-env";
            settings.GlobalTags = new Dictionary<string, string> { { "Updated-key", "Updated Value" } };
            Tracer.Configure(settings);
            LogAndAssertCurrentSettings(Tracer.Instance, "Reconfigured", settings);

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
            LogAndAssertCurrentSettings(Tracer.Instance, "HttpDisabled", settings);

            // send a trace with it disabled
            using (Tracer.Instance.StartActive($"Manual-{++count}.HttpDisabled"))
            {
                await SendHttpRequest("HttpDisabled");
            }

            await Tracer.Instance.ForceFlushAsync();

            // go back to the defaults
            settings = TracerSettings.FromDefaultSources();
            Tracer.Configure(settings);
            LogAndAssertCurrentSettings(Tracer.Instance, "DefaultsReinstated", settings);
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

            using (Tracer.Instance.StartActive($"Manual-{++count}.EventSdkV2.Success.Outer"))
            {
                using var s2 = Tracer.Instance.StartActive($"Manual-{count}.EventSdkV2.Success.Inner");
                EventTrackingSdkV2.TrackUserLoginSuccess(
                    "my-login",
                    new UserDetails("my-id")
                    {
                        Email = "test@test.fr",
                        Name = "test-name",
                        Role = "test-role",
                        Scope = "test-scope",
                        SessionId = "abc123"
                    },
                    new Dictionary<string, string>
                    {
                        { "key-1", "val-1" },
                        { "key-2", "val-2" }
                    });
                await SendHttpRequest("Ext");
            }

            using (Tracer.Instance.StartActive($"Manual-{++count}.EventSdkV2.Failure.Outer"))
            {
                using var s2 = Tracer.Instance.StartActive($"Manual-{count}.EventSdkV2.Failure.Inner");
                EventTrackingSdkV2.TrackUserLoginFailure(
                    "my-login",
                    true,
                    new UserDetails("my-id")
                    {
                        Email = "test@test.fr",
                        Name = "test-name",
                        Role = "test-role",
                        Scope = "test-scope",
                        SessionId = "abc123"
                    },
                    new Dictionary<string, string>
                    {
                        { "key-1", "val-1" },
                        { "key-2", "val-2" }
                    });

                await SendHttpRequest("Ext");
            }

            // Custom context
            var parent = new SpanContext(traceId: 1234567, 7654321, SamplingPriority.AutoKeep, "manual-parent");
            var createSpan = new SpanCreationSettings { FinishOnClose = false, StartTime = DateTimeOffset.Now.AddHours(-1), Parent = parent, };
            using (var s1 = Tracer.Instance.StartActive($"Manual-{++count}.CustomContext", createSpan))
            {
                s1.Span.ServiceName = Tracer.Instance.DefaultServiceName;
                await SendHttpRequest("CustomContext");

                // Test injection
                Dictionary<string, List<string>> headers = new();
                new SpanContextInjector().Inject(
                    headers,
                    setter: (dict, key, value) => headers[key] = new List<string> { value },
                    s1.Span.Context);
                var context = new SpanContextExtractor().Extract(
                    headers,
                    getter: (dict, key) => dict.TryGetValue(key, out var values) ? values : Enumerable.Empty<string>());

                Expect(context is not null, "Extracted context should not be null");
                Expect(s1.Span.Context.SpanId == context?.SpanId, "SpanId should be extracted");
                Expect(s1.Span.Context.TraceId == context?.TraceId, "TraceId should be extracted");

                // Test DSM injection
                Dictionary<string, List<string>> dsmHeaders = new();
                new SpanContextInjector().InjectIncludingDsm(
                    dsmHeaders,
                    setter: (dict, key, value) => dsmHeaders[key] = new List<string> { value },
                    s1.Span.Context,
                    "messageType",
                    "messageId");
                var dsmContext = new SpanContextExtractor().ExtractIncludingDsm(
                    dsmHeaders,
                    getter: (dict, key) => dict.TryGetValue(key, out var values) ? values : Enumerable.Empty<string>(),
                    "messageType",
                    "messageId");
                Expect(dsmContext is not null, "Extracted context should not be null");
                Expect(s1.Span.Context.SpanId == dsmContext?.SpanId, "SpanId should be extracted");
                Expect(s1.Span.Context.TraceId == dsmContext?.TraceId, "TraceId should be extracted");

                // Test that we handle incorrect (null returning) implementations
                var nullContext1 = new SpanContextExtractor().Extract(
                    headers,
                    getter: (dict, key) => null); // Always return null
                Expect(nullContext1 is null, "Extracted context should be null");

                var nullContext2 = new SpanContextExtractor().ExtractIncludingDsm(
                    dsmHeaders,
                    getter: (dict, key) => null, // Always return null
                    "messageType",
                    "messageId");
                Expect(nullContext2 is null, "Extracted context should be null");

                // Exceptions thrown in the faulty injector/extractor will not bubble up
                // as they're caught in the integration, but they will write error logs if we don't handle them
                // so will be caught in the CheckLogsForErrors stage in CI.
                new SpanContextInjector().Inject(
                    headers,
                    setter: (dict, key, value) => throw new Exception("Throwing exception that should be ignored"),
                    s1.Span.Context);
                new SpanContextInjector().InjectIncludingDsm(
                    headers,
                    setter: (dict, key, value) => throw new Exception("Throwing exception that should be ignored"),
                    s1.Span.Context,
                    "messageType",
                    "messageId");
            }

            // Manually disable debug logs
            GlobalSettings.SetDebugEnabled(false);

            // Try to reconfigure tracer to use UDS on Windows. This should throw an exception
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var invalidSettings = TracerSettings.FromDefaultSources();
                    invalidSettings.ServiceName = "InvalidSettings";
                    invalidSettings.AgentUri = new Uri("unix://apm.socket");
                    Tracer.Configure(invalidSettings);
                    ThrowIf(true, "We should not be able to configure the tracer with UDS on Windows");
                }
                catch
                {
                    LogAndAssertCurrentSettings(Tracer.Instance, "RejectedInvalidSettings", settings);
                }
            }

            // Force flush
            await Tracer.Instance.ForceFlushAsync();

            // Force process to end, otherwise the background listener thread lives forever in .NET Core.
            // Apparently listener.GetContext() doesn't throw an exception if listener.Stop() is called,
            // like it does in .NET Framework.
            server.Dispose();
            Environment.Exit(0);
            return;

            async Task<HttpResponseMessage> SendHttpRequest(string name)
            {
                mutex.Reset();
                var q = $"{count}.{name}";
                using var scope = Tracer.Instance.StartActive($"Manual-{q}.HttpClient");
                var responseMessage = await client.GetAsync(url + $"?q={q}");

                Console.WriteLine("Received response for client.GetAsync(String)");

                if (!mutex.Wait(30_000))
                {
                    throw new Exception($"Timed out waiting for response to request: Manual-{q}.HttpClient");
                }

                return responseMessage;
            }

            void HandleHttpRequests(HttpListenerContext context)
            {
                try
                {
                    var query = context.Request.QueryString["q"];
                    using (var scope = Tracer.Instance.StartActive($"Manual-{query}.HttpListener"))
                    {
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

                    mutex.Set();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    context.Response.Close();
                    mutex.Set();
                    throw;
                }
            }

            void LogAndAssertCurrentSettings(Tracer tracer, string step, TracerSettings expected = null)
            {
                var settings = tracer.Settings;
                Console.WriteLine($"Current tracer settings for {step}: ");

                WriteLog(settings.Environment);
                WriteLog(settings.ServiceName);
                WriteLog(settings.ServiceVersion);
                var globalTags = string.Join(". ", settings.GlobalTags.Select(x => $"{x.Key}:{x.Value}"));
                WriteLog(globalTags);

                if (expected is not null)
                {
                    Expect(settings.Environment == expected.Environment);
                    Expect(settings.ServiceName == expected.ServiceName);
                    Expect(settings.ServiceVersion == expected.ServiceVersion);
                    Expect(settings.GlobalTags.Count == expected.GlobalTags.Count);
                }

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
        }
    }
}

class CustomException : Exception
{
}

class InstrumentationErrorException(string condition, bool expected)
    : Exception($"Instrumentation of manual API error: {condition} should be {expected} when automatic instrumentation is running correctly, but was {!expected}")
{
}
