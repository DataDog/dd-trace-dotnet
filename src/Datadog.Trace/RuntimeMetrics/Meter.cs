namespace Datadog.Trace.RuntimeMetrics
{
    internal class Meter
    {
        private long _value;
        private long _count;

        public void Mark(long value)
        {
            lock (this)
            {
                _value += value;
                _count++;
            }
        }

        public long Clear()
        {
            lock (this)
            {
                if (_count == 0)
                {
                    return 0;
                }

                var result = _value / _count;

                _value = 0;
                _count = 0;

                return result;
            }
        }
    }
}
