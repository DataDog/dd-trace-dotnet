using System;
using System.IO;

namespace dd_prof_etw_replay
{
    public class RecordReader
    {
        private readonly BinaryReader _reader;
        private readonly IEventDumper _dumper;

        public RecordReader(BinaryReader reader, IEventDumper dumper)
        {
            if (dumper == null)
            {
                throw new ArgumentNullException(nameof(dumper));
            }

            _reader = reader;
            _dumper = dumper;
        }

        public void ReadRecord()
        {
            UInt64 timestamp = _reader.ReadUInt64();
            UInt32 tid = _reader.ReadUInt32();
            UInt32 version = _reader.ReadUInt32();
            UInt64 keyword = _reader.ReadUInt64();
            byte level = _reader.ReadByte();
            UInt32 id = _reader.ReadUInt32();
            UInt32 size = _reader.ReadUInt32();
            byte[] data = _reader.ReadBytes((int)size);
            _dumper.DumpEvent(timestamp, tid, version, keyword, level, id, data);
        }
    }
}
