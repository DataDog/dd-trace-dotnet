using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.VersionConflict_2x
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "inode")
            {
                CheckInodeLookup();
                return;
            }

            using var server = WebServer.Start(out var url);
            
            server.RequestHandler = context =>
            {
                var traceId = context.Request.Headers[HttpHeaderNames.TraceId];
                var spanId = context.Request.Headers[HttpHeaderNames.ParentId];
                var samplingPriority = context.Request.Headers[HttpHeaderNames.SamplingPriority];

                Console.WriteLine($"{traceId}/{spanId}/{samplingPriority}");

                var payload = Encoding.UTF8.GetBytes("OK");

                context.Response.ContentEncoding = Encoding.UTF8;
                context.Response.ContentLength64 = payload.Length;
                context.Response.OutputStream.Write(payload, 0, payload.Length);
                context.Response.Close();
            };

            // Attempt to access the ActiveScope while the automatic ActiveScope is null
            var activeScope = Tracer.Instance.ActiveScope;
            if (activeScope is not null)
            {
                throw new InvalidOperationException("Tracer.Instance.ActiveScope should be null");
            }

            using (var scope = Tracer.Instance.StartActive("Manual"))
            {
                scope.Span.SetTag(Tags.SamplingPriority, "UserKeep");

                using (var client = new HttpClient())
                {
                    _ = await client.GetStringAsync(url + "/a");

                    scope.Span.SetTag(Tags.SamplingPriority, "UserReject");

                    _ = await client.GetStringAsync(url + "/b");
                }
            }

            if (args.Length > 0 && args[0] == "wait")
            {
                Console.WriteLine($"Waiting - PID: {Process.GetCurrentProcess().Id}");
                Thread.Sleep(Timeout.Infinite);
            }
        }

        private static void CheckInodeLookup()
        {
            // Register the old tracer with the automatic tracer before initializing Tracer.Instance.
            // Process spans will then ask the old tracer for its sampling priority during initialization.
            var tracerAssembly = typeof(Tracer).Assembly;
            var distributedTracer = tracerAssembly.GetType("Datadog.Trace.ClrProfiler.DistributedTracer", throwOnError: true);
            RuntimeHelpers.RunClassConstructor(distributedTracer.TypeHandle);

            // Invoke the old tracer's inode lookup even when /proc/self/cgroup contains a container ID.
            // This must not generate a process span or recursively initialize the manual tracer.
            var metadata = tracerAssembly.GetType("Datadog.Trace.PlatformHelpers.ContainerMetadata", throwOnError: true);
            var tryGetInode = metadata.GetMethod("TryGetInode", BindingFlags.Static | BindingFlags.NonPublic);
            object[] arguments = ["/proc/self/ns/cgroup", 0L];
            var success = (bool)tryGetInode.Invoke(null, arguments);
            // BusyBox stat does not support --printf, and Windows may not have stat at all.
            // Both the successful lookup and the failure path must preserve the return/out values.
            if (success ? (long)arguments[1] <= 0 : (long)arguments[1] != 0)
            {
                throw new InvalidOperationException("The inode lookup returned inconsistent results.");
            }

            Console.WriteLine($"Inode lookup succeeded: {success}");

            // A normal process on the same thread must still produce a span after the lookup completes.
            using var scope = Tracer.Instance.StartActive("Manual");
            var startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                ? new ProcessStartInfo("cmd.exe", "/c echo normal-process")
                                : new ProcessStartInfo("/bin/echo", "normal-process");
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            using var process = Process.Start(startInfo);
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("The normal process failed.");
            }
        }
    }
}
