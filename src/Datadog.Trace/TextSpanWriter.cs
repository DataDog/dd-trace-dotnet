using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Datadog.Trace
{
    internal class TextSpanWriter : ISpanWriter
    {
        private readonly TextWriter _textWriter;
        private int _traceCount;

        public TextSpanWriter(TextWriter writer)
        {
            _textWriter = writer;
        }

        public void WriteTrace(List<Span> trace)
        {
            _traceCount++;
            _textWriter.WriteLine($"Trace {_traceCount}");
            _textWriter.WriteLine("----------------");

            foreach (Span span in trace)
            {
                _textWriter.Write(span.ToString());
                _textWriter.WriteLine("----------------");
            }
        }

        public Task FlushAndCloseAsync()
        {
            _textWriter.Close();
            return Task.FromResult(true);
        }
    }
}
