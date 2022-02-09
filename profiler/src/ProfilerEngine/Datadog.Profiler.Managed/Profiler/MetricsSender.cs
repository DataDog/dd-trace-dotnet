// <copyright file="MetricsSender.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Datadog.Configuration;

namespace Datadog.Profiler
{
    internal class MetricsSender
    {
        private readonly byte[] _tagsAsBytes;
        private readonly byte[] _colonAsBytes;
        private readonly byte[] _metricTypeAndTagsSeparatorAsBytes;
        private readonly Socket _socket;
        private readonly IPEndPoint _statsdAgentEndpoint;
        private readonly byte[] _buffer;
        private readonly StringBuilder _additionalTagsBuilder;

        public MetricsSender(IProductConfiguration config)
        {
            var tags = $"profiler_version:{config.DDDataTags_Version},service_name:{config.DDDataTags_Service},environment:{config.DDDataTags_Env}";
            _tagsAsBytes = Encoding.UTF8.GetBytes(tags);

            _colonAsBytes = Encoding.UTF8.GetBytes(":");
            _metricTypeAndTagsSeparatorAsBytes = Encoding.UTF8.GetBytes("|c|#");

            _socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            // We consider the statsd agent to be installed locally (for now)
            _statsdAgentEndpoint = CreateEndpoint("127.0.0.1", config.Metrics_StatsdAgent_Port);
            _buffer = new byte[1024];
            _additionalTagsBuilder = new StringBuilder();
        }

        public void SendMetric(string metricName, long value, string[] tags = null)
        {
            // expected format metric_name:value|c|#tag1,tag2,tag3...

            int offset = Encoding.UTF8.GetBytes(metricName, 0, metricName.Length, _buffer, 0);

            _colonAsBytes.CopyTo(_buffer, offset);
            offset += _colonAsBytes.Length;

            var valueAsStr = value.ToString();
            offset += Encoding.UTF8.GetBytes(valueAsStr, 0, valueAsStr.Length, _buffer, offset);

            _metricTypeAndTagsSeparatorAsBytes.CopyTo(_buffer, offset);
            offset += _metricTypeAndTagsSeparatorAsBytes.Length;

            _tagsAsBytes.CopyTo(_buffer, offset);
            offset += _tagsAsBytes.Length;

            if (tags != null && tags.Length > 0)
            {
                _additionalTagsBuilder.Clear();

                foreach (string tag in tags)
                {
                    _additionalTagsBuilder.Append(",");
                    _additionalTagsBuilder.Append(tag);
                }

                string additionalTags = _additionalTagsBuilder.ToString();
                offset += Encoding.UTF8.GetBytes(additionalTags, 0, additionalTags.Length, _buffer, offset);
            }

            _socket.SendTo(_buffer, offset, SocketFlags.None, _statsdAgentEndpoint);
        }

        private IPEndPoint CreateEndpoint(string hostname, int port)
        {
            IPAddress ip = Dns.GetHostAddresses(hostname).First(a => a.AddressFamily == AddressFamily.InterNetwork);
            return new IPEndPoint(ip, port);
        }
    }
}
