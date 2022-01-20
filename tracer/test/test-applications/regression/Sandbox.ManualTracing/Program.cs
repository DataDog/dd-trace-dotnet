using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.Configuration;

namespace Sandbox.ManualTracing
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Set the minimum permissions needed to run code in the new AppDomain
            PermissionSet permSet = new PermissionSet(PermissionState.None);
            permSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution)); // Necessary to run code.
            // permSet.AddPermission(new WebPermission(PermissionState.Unrestricted)); // Necessary to emit traces. If commented out, the Tracer will not send Traces but it should not crash.
            // permSet.AddPermission(new FileIOPermission(PermissionState.Unrestricted)); // Necessary to access the file system.

            var remote = AppDomain.CreateDomain("Remote", null, AppDomain.CurrentDomain.SetupInformation, permSet);

            try
            {
                remote.DoCallBack(CreateManualTraces);
                return (int)ExitCode.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"We have encountered an exception, the smoke test fails: {ex.Message}");
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }
            finally
            {
                AppDomain.Unload(remote);
            }
        }

        public static void CreateManualTraces()
        {
            // Create a configuration source so we can enable runtime metrics without relying on environment variables,
            // which would require an additional permission to be added
            var configurationSource = new NameValueConfigurationSource(new NameValueCollection()
            {
                { "DD_RUNTIME_METRICS_ENABLED", "1" }
            });

            var tracerSettings = new TracerSettings(configurationSource);
            Tracer.Configure(tracerSettings);

            for (int i = 0; i < 5; i++)
            {
                EmitCustomSpans();
                Thread.Sleep(2000);
            }
        }

        private static void EmitCustomSpans()
        {
            using (Tracer.Instance.StartActive("custom-span"))
            {
                Thread.Sleep(1500);

                using (Tracer.Instance.StartActive("inner-span"))
                {
                    Thread.Sleep(1500);
                }
            }
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
