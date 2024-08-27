using System;

namespace dd_prof_etw_replay
{
    public class EventDumper : IEventDumper
    {
        public void DumpEvent(ulong timestamp, uint tid, uint version, ulong keyword, byte level, uint id, Span<byte> pEventData)
        {
            Console.WriteLine($"{timestamp,12} | {tid,6} - [{keyword,8:x}, {level,2}] = ({id,3}, {version,2}) with {pEventData.Length} bytes");
        }
    }
}
