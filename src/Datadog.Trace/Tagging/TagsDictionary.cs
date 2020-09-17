using System.Collections.Generic;
using System.Text;
using System.Threading;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Tagging
{
    internal abstract class TagsDictionary : Dictionary<string, string>, ITags
    {
        private static readonly IProperty<string>[] EmptyTags = new IProperty<string>[0];
        private static readonly IProperty<double?>[] EmptyMetrics = new IProperty<double?>[0];

        private Dictionary<string, double> _metrics;

        protected Dictionary<string, double> Metrics => Volatile.Read(ref _metrics);

        public string GetTag(string key)
        {
            foreach (var property in GetAdditionalTags())
            {
                if (property.Key == key)
                {
                    return property.Getter(this);
                }
            }

            return TryGetValue(key, out var value) ? value : null;
        }

        public double? GetMetric(string key)
        {
            foreach (var property in GetAdditionalMetrics())
            {
                if (property.Key == key)
                {
                    return property.Getter(this);
                }
            }

            var metrics = Metrics;

            if (metrics == null)
            {
                return null;
            }

            return metrics.TryGetValue(key, out var value) ? value : (double?)null;
        }

        public void SetTag(string key, string value)
        {
            foreach (var property in GetAdditionalTags())
            {
                if (property.Key == key)
                {
                    property.Setter(this, value);
                    return;
                }
            }

            lock (this)
            {
                if (value == null)
                {
                    Remove(key);
                    return;
                }

                this[key] = value;
            }
        }

        public void SetMetric(string key, double? value)
        {
            foreach (var property in GetAdditionalMetrics())
            {
                if (property.Key == key)
                {
                    property.Setter(this, value);
                    return;
                }
            }

            var metrics = Metrics;

            if (metrics == null)
            {
                var newMetrics = new Dictionary<string, double>();
                metrics = Interlocked.CompareExchange(ref _metrics, new Dictionary<string, double>(), null) ?? newMetrics;
            }

            lock (metrics)
            {
                if (value == null)
                {
                    metrics.Remove(key);
                    return;
                }

                metrics[key] = value.Value;
            }
        }

        public int SerializeTo(ref byte[] bytes, int offset)
        {
            offset += WriteTags(ref bytes, offset);
            offset += WriteMetrics(ref bytes, offset);

            return offset;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            lock (this)
            {
                foreach (var pair in this)
                {
                    sb.Append($"{pair.Key} (tag):{pair.Value},");
                }
            }

            var metrics = Metrics;

            if (metrics != null)
            {
                lock (metrics)
                {
                    foreach (var pair in metrics)
                    {
                        sb.Append($"{pair.Key} (metric):{pair.Value}");
                    }
                }
            }

            foreach (var property in GetAdditionalTags())
            {
                var value = property.Getter(this);

                if (value != null)
                {
                    sb.Append($"{property.Key} (tag):{value},");
                }
            }

            foreach (var property in GetAdditionalMetrics())
            {
                var value = property.Getter(this);

                if (value != null)
                {
                    sb.Append($"{property.Key} (metric):{value.Value},");
                }
            }

            return sb.ToString();
        }

        protected virtual IProperty<string>[] GetAdditionalTags()
        {
            return EmptyTags;
        }

        protected virtual IProperty<double?>[] GetAdditionalMetrics()
        {
            return EmptyMetrics;
        }

        private int WriteTags(ref byte[] bytes, int offset)
        {
            offset += MessagePackBinary.WriteString(ref bytes, offset, "meta");

            int headerOffset = offset;
            int count;

            lock (this)
            {
                count = Count;

                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, count);

                foreach (var pair in this)
                {
                    offset += MessagePackBinary.WriteString(ref bytes, offset, pair.Key);
                    offset += MessagePackBinary.WriteString(ref bytes, offset, pair.Value);
                }
            }

            foreach (var property in GetAdditionalTags())
            {
                var value = property.Getter(this);

                if (value != null)
                {
                    count++;
                    offset += MessagePackBinary.WriteString(ref bytes, offset, property.Key);
                    offset += MessagePackBinary.WriteString(ref bytes, offset, value);
                }
            }

            // Write updated count
            MessagePackBinary.WriteMapHeader(ref bytes, headerOffset, count);

            return offset;
        }

        private int WriteMetrics(ref byte[] bytes, int offset)
        {
            offset += MessagePackBinary.WriteString(ref bytes, offset, "metrics");

            var metrics = Metrics;

            int headerOffset = offset;
            int count;

            if (metrics != null)
            {
                lock (metrics)
                {
                    count = metrics.Count;

                    offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, count);

                    foreach (var pair in metrics)
                    {
                        offset += MessagePackBinary.WriteString(ref bytes, offset, pair.Key);
                        offset += MessagePackBinary.WriteDouble(ref bytes, offset, pair.Value);
                    }
                }
            }
            else
            {
                count = 0;
                offset += MessagePackBinary.WriteMapHeader(ref bytes, offset, count);
            }

            foreach (var property in GetAdditionalMetrics())
            {
                var value = property.Getter(this);

                if (value != null)
                {
                    count++;
                    offset += MessagePackBinary.WriteString(ref bytes, offset, property.Key);
                    offset += MessagePackBinary.WriteDouble(ref bytes, offset, value.Value);
                }
            }

            // Write updated count
            MessagePackBinary.WriteMapHeader(ref bytes, headerOffset, count);

            return offset;
        }
    }
}
