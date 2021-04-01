using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Agent.Transports
{
    internal class AgentlessInterop
    {
        private static InitDelegateType _initDelegate;

        private static SendTracesDelegateType _sendTracesDelegateType;

        private delegate void InitDelegateType();

        private unsafe delegate void SendTracesDelegateType(byte* data, int length);

        // [DllImport("agent.dll", CallingConvention = CallingConvention.StdCall)]
        // public static extern void Init();
        // [DllImport("agent.dll", CallingConvention = CallingConvention.StdCall)]
        // public static extern unsafe void SendTraces(byte* data, int length);

        public static void InitializeTraceAgent()
        {
            _initDelegate.Invoke();
        }

        public static unsafe void SendTraces(byte* data, int length)
        {
            _sendTracesDelegateType.Invoke(data, length);
        }

        public static void LoadAgentless()
        {
            var agentLibraryPath = Environment.GetEnvironmentVariable("DD_TRACE_AGENT_DLL_PATH");
            if (agentLibraryPath != null)
            {
                var hModule = LoadLibrary(agentLibraryPath);
                var functionAddress = GetProcAddress(hModule, "Init");
                _initDelegate = (InitDelegateType)Marshal.GetDelegateForFunctionPointer(functionAddress, typeof(InitDelegateType));
                functionAddress = GetProcAddress(hModule, "SendTraces");
                _sendTracesDelegateType = (SendTracesDelegateType)Marshal.GetDelegateForFunctionPointer(functionAddress, typeof(SendTracesDelegateType));
            }
        }

        [DllImport("Kernel32.dll")]
        private static extern IntPtr LoadLibrary(string path);

        [DllImport("Kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}
