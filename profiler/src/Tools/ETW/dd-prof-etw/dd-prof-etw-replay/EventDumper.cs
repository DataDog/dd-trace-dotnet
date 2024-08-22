using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dd_prof_etw_replay
{
    public class EventDumper : IEventDumper
    {
        public void DumpEvent(ulong timestamp, uint tid, uint version, ulong keyword, byte level, uint id, Span<byte> pEventData)
        {

        }
    }
}
