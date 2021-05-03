using System;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent.Transports
{
    internal class AgentlessInterop
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AgentlessInterop>();

        [DllImport("trace-agent.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void Init();

        [DllImport("trace-agent.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe void SendTraces(byte* data, int length);

        public static void InitializeTraceAgent()
        {
            try
            {
                Init();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to initialize agent library");
            }
        }

        public static unsafe void MarshalTraces(byte* data, int length)
        {
            try
            {
                SendTraces(data, length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to send traces");
            }
        }

        public static void LoadAgentless()
        {
            try
            {
                string agentLibraryDirectory;

                if (Environment.Is64BitProcess)
                {
                    agentLibraryDirectory = Environment.GetEnvironmentVariable("DD_AGENTLESS_DIRECTORY_X64");
                }
                else
                {
                    agentLibraryDirectory = Environment.GetEnvironmentVariable("DD_AGENTLESS_DIRECTORY_X86");
                }

                if (string.IsNullOrEmpty(agentLibraryDirectory))
                {
                    Log.Error("Agentless environment variable is missing.");
                    return;
                }

                // This list can be expanded once more capabilities are added to agentless
                var librariesToLoad = new[]
                {
                    "trace-agent.dll"
                };

                foreach (var library in librariesToLoad)
                {
                    var module = LoadLibrary(Path.Combine(agentLibraryDirectory, library));
                    if (module == IntPtr.Zero)
                    {
                        Log.Warning("Unable to locate {library} at {location}", library, agentLibraryDirectory);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to load agent library");
            }
        }

        [DllImport("Kernel32.dll")]
        private static extern IntPtr LoadLibrary(string path);
    }
}
