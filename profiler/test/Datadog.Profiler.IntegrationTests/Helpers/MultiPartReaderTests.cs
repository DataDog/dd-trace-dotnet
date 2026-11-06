// <copyright file="MultiPartReaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    [Trait("Category", "TestInfrastructure")]
    public class MultiPartReaderTests
    {
        private const string Boundary = "uHOg3S";
        private const string EventJson = "{\"service\":\"computer01\"}";
        private const string MetricsJson = "[[\"dotnet_managed_threads\",3]]";
        private const string HistogramJson = "[[\"System.String\",1,16]]";
        private const string ReferenceTreeJson = "{\"v\":1}";

        private static readonly byte[] PprofBytes = { 0x00, 0x01, 0xFF, 0x7F };
        private static readonly byte[] ReferenceTreeBytes = { 0x01, 0x02, 0x03, 0x04 };

        [Fact]
        public void TryParse_SampleProfileUpload_ReadsPartsByFileName()
        {
            var parsed = MultiPartReader.TryParse(ContentType(), SampleProfileUpload(), Encoding.UTF8, out var upload);

            parsed.Should().BeTrue();
            upload.Files.Should().HaveCount(6);

            upload.TryGetFile("event.json", out var eventPart).Should().BeTrue();
            eventPart.Name.Should().Be("event");
            eventPart.FileName.Should().Be("event.json");
            eventPart.ContentType.Should().Be("application/json");
            eventPart.ReadText().Should().Be(EventJson);
            using (var eventJson = eventPart.ReadJson())
            {
                eventJson.RootElement.GetProperty("service").GetString().Should().Be("computer01");
            }

            upload.TryGetFile("auto.pprof", out var pprofPart).Should().BeTrue();
            pprofPart.Name.Should().Be("auto.pprof");
            pprofPart.ContentType.Should().Be("application/octet-stream");
            pprofPart.ReadBytes().Should().Equal(PprofBytes);

            upload.TryGetFile("metrics.json", out var metricsPart).Should().BeTrue();
            using (var metrics = metricsPart.ReadJson())
            {
                metrics.RootElement[0][0].GetString().Should().Be("dotnet_managed_threads");
                metrics.RootElement[0][1].GetDouble().Should().Be(3);
            }

            upload.ContainsFile("histogram.json").Should().BeTrue();
            upload.TryGetFile("histogram.json", out var histogramPart).Should().BeTrue();
            using (var histogram = histogramPart.ReadJson())
            {
                histogram.RootElement[0][0].GetString().Should().Be("System.String");
            }

            upload.ContainsFile("reference_tree.json").Should().BeTrue();
            upload.TryGetFile("reference_tree.json", out var treeJsonPart).Should().BeTrue();
            treeJsonPart.ReadText().Should().Be(ReferenceTreeJson);

            upload.ContainsFile("reference_tree.bin").Should().BeTrue();
            upload.TryGetFile("reference_tree.bin", out var treeBinPart).Should().BeTrue();
            treeBinPart.ReadBytes().Should().Equal(ReferenceTreeBytes);

            upload.ContainsFile("missing.json").Should().BeFalse();
            upload.TryGetFile("missing.json", out _).Should().BeFalse();
            upload.ContainsFile("Metrics.json").Should().BeFalse();
        }

        [Fact]
        public void TryParse_NonMultipartContentType_ReturnsFalse()
        {
            var parsed = MultiPartReader.TryParse("application/json", SampleProfileUpload(), Encoding.UTF8, out var upload);

            parsed.Should().BeFalse();
            upload.Should().BeNull();
        }

        [Fact]
        public void TryParse_MissingBoundary_ReturnsFalse()
        {
            var parsed = MultiPartReader.TryParse("multipart/form-data", SampleProfileUpload(), Encoding.UTF8, out var upload);

            parsed.Should().BeFalse();
            upload.Should().BeNull();
        }

        [Fact]
        public void TryParse_TruncatedBody_ReturnsFalse()
        {
            var truncated = Encoding.UTF8.GetBytes(
                "--" + Boundary + "\r\n" +
                "content-disposition: form-data; name=\"event\"; filename=\"event.json\"\r\n" +
                "\r\n" +
                "{}");

            var parsed = MultiPartReader.TryParse(ContentType(), truncated, Encoding.UTF8, out var upload);

            parsed.Should().BeFalse();
            upload.Should().BeNull();
        }

        private static string ContentType()
        {
            return "multipart/form-data; boundary=" + Boundary;
        }

        private static byte[] SampleProfileUpload()
        {
            using var body = new MemoryStream();
            WritePart(body, first: true, "application/json", "event", "event.json", Encoding.UTF8.GetBytes(EventJson));
            WritePart(body, first: false, "application/octet-stream", "auto.pprof", "auto.pprof", PprofBytes);
            WritePart(body, first: false, "application/json", "metrics.json", "metrics.json", Encoding.UTF8.GetBytes(MetricsJson));
            WritePart(body, first: false, "application/json", "histogram.json", "histogram.json", Encoding.UTF8.GetBytes(HistogramJson));
            WritePart(body, first: false, "application/json", "reference_tree.json", "reference_tree.json", Encoding.UTF8.GetBytes(ReferenceTreeJson));
            WritePart(body, first: false, "application/octet-stream", "reference_tree.bin", "reference_tree.bin", ReferenceTreeBytes);
            WriteAscii(body, "\r\n--" + Boundary + "--");
            return body.ToArray();
        }

        private static void WritePart(Stream body, bool first, string contentType, string name, string fileName, byte[] content)
        {
            // Parts are separated by CRLF + boundary. The first boundary is at the start of the body.
            WriteAscii(body, first ? "--" + Boundary + "\r\n" : "\r\n--" + Boundary + "\r\n");
            WriteAscii(body, "content-type: " + contentType + "\r\n");
            WriteAscii(body, "content-disposition: form-data; name=\"" + name + "\"; filename=\"" + fileName + "\"\r\n");
            WriteAscii(body, "\r\n");
            body.Write(content, 0, content.Length);
        }

        private static void WriteAscii(Stream body, string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            body.Write(bytes, 0, bytes.Length);
        }
    }
}
