using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent.Transports
{
    internal class AgentlessInterop
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AgentlessInterop>();

        // private static InitDelegateType _initDelegate;

        // private static SendTracesDelegateType _sendTracesDelegateType;

        // private delegate void InitDelegateType();

        // private unsafe delegate void SendTracesDelegateType(byte* data, int length);

        [DllImport("agent.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void Init();

        [DllImport("agent.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe void SendTraces(byte* data, int length);

        public static void InitializeTraceAgent()
        {
            try
            {
                if (NativeMethods.IsProfilerAttached())
                {
                    Environment.SetEnvironmentVariable("DD_PINVOKE_VERIFIED", "Profiler is attached and verified with PINVOKE call.");
                }

                Init();
                // _initDelegate.Invoke();
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
                // _sendTracesDelegateType.Invoke(data, length);
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
                var agentLibraryPath = Environment.GetEnvironmentVariable("DD_TRACE_AGENT_DLL_PATH");
                if (agentLibraryPath != null)
                {
                    // Assembly.LoadFile(agentLibraryPath);
                    var agentModule = LoadLibrary(agentLibraryPath);
                    if (agentModule == IntPtr.Zero)
                    {
                        Log.Warning("Unable to locate agent.dll at {location}", agentLibraryPath);
                    }

                    // var hModule = LoadLibrary(agentLibraryPath);
                    // var functionAddress = GetProcAddress(hModule, "Init");
                    // _initDelegate = (InitDelegateType)Marshal.GetDelegateForFunctionPointer(functionAddress, typeof(InitDelegateType));
                    // functionAddress = GetProcAddress(hModule, "SendTraces");
                    // _sendTracesDelegateType = (SendTracesDelegateType)Marshal.GetDelegateForFunctionPointer(functionAddress, typeof(SendTracesDelegateType));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to load agent library");
            }
        }

        [DllImport("Kernel32.dll")]
        private static extern IntPtr LoadLibrary(string path);

        // [DllImport("Kernel32.dll")]
        // private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}
