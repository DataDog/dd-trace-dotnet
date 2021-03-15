using System.Runtime.InteropServices;

namespace Datadog.Trace.Agent.Transports
{
    internal class AgentlessInterop
    {
        [DllImport("agent.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void Init();

        [DllImport("agent.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe void SendTraces(byte* data, int length);
    }
}
