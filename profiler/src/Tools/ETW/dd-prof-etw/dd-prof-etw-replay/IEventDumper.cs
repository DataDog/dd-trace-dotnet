using System;

namespace dd_prof_etw_replay
{
    public interface IEventDumper
    {
        public abstract void DumpEvent(
        UInt64 timestamp,
        UInt32 tid,
        UInt32 version,
        UInt64 keyword,
        byte level,
        UInt32 id,
        Span<byte> pEventData);
    }
}
