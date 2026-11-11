// <copyright file="SyntheticProfile.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using Google.Protobuf;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using Perftools.Profiles;
using ZstdSharp;
using PprofSample = Perftools.Profiles.Sample;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles.Tests
{
    internal sealed class SyntheticProfile
    {
        private readonly Profile _profile = new Profile();

        public SyntheticProfile()
        {
            AddString(string.Empty);
        }

        public SyntheticProfile SampleType(string name, string unit)
        {
            _profile.SampleType.Add(new ValueType
            {
                Type = AddString(name),
                Unit = AddString(unit),
            });
            return this;
        }

        public ulong Function(string function, string filename)
        {
            var id = (ulong)_profile.Function.Count + 1;
            _profile.Function.Add(new Function
            {
                Id = id,
                Name = AddString(TestStack.Text(function)),
                Filename = AddString(filename),
            });
            return id;
        }

        public ulong Location(ulong functionId, long line)
        {
            var id = (ulong)_profile.Location.Count + 1;
            var location = new Location { Id = id };
            location.Line.Add(new Line { FunctionId = functionId, Line_ = line });
            _profile.Location.Add(location);
            return id;
        }

        public ulong InlinedLocation(ulong inlineeId, long inlineeLine, ulong outerId, long outerLine)
        {
            var id = (ulong)_profile.Location.Count + 1;
            var location = new Location { Id = id };
            location.Line.Add(new Line { FunctionId = inlineeId, Line_ = inlineeLine });
            location.Line.Add(new Line { FunctionId = outerId, Line_ = outerLine });
            _profile.Location.Add(location);
            return id;
        }

        public SyntheticProfile AddSample(long[] values, ulong[] locations, params SampleLabel[] labels)
        {
            var sample = new PprofSample();
            foreach (var value in values)
            {
                sample.Value.Add(value);
            }

            foreach (var location in locations)
            {
                sample.LocationId.Add(location);
            }

            foreach (var label in labels)
            {
                var proto = new Label { Key = AddString(label.Name) };
                if (label.IsNumeric)
                {
                    proto.Num = label.Number;
                    proto.NumUnit = AddString(label.Unit);
                    if (label.Text != null)
                    {
                        proto.Str = AddString(label.Text);
                    }
                }
                else
                {
                    proto.Str = AddString(label.Text);
                }

                sample.Label.Add(proto);
            }

            _profile.Sample.Add(sample);
            return this;
        }

        public string WriteRaw(string directory, string fileName)
        {
            var path = Prepare(directory, fileName);
            File.WriteAllBytes(path, _profile.ToByteArray());
            return path;
        }

        public string WriteLz4(string directory, string fileName)
        {
            var path = Prepare(directory, fileName);
            var bytes = _profile.ToByteArray();
            using (var file = File.Create(path))
            using (var encoder = LZ4Stream.Encode(file, LZ4Level.L00_FAST, 0, true))
            {
                encoder.Write(bytes, 0, bytes.Length);
            }

            return path;
        }

        public string WriteZstd(string directory, string fileName)
        {
            var path = Prepare(directory, fileName);
            var bytes = _profile.ToByteArray();
            using (var file = File.Create(path))
            using (var encoder = new CompressionStream(file))
            {
                encoder.Write(bytes, 0, bytes.Length);
            }

            return path;
        }

        private static string Prepare(string directory, string fileName)
        {
            var path = Path.Combine(directory, fileName);
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return path;
        }

        private long AddString(string value)
        {
            for (var i = 0; i < _profile.StringTable.Count; i++)
            {
                if (_profile.StringTable[i] == value)
                {
                    return i;
                }
            }

            _profile.StringTable.Add(value);
            return _profile.StringTable.Count - 1;
        }

        internal readonly struct SampleLabel
        {
            private SampleLabel(string name, string text, bool isNumeric, long number, string unit)
            {
                Name = name;
                Text = text;
                IsNumeric = isNumeric;
                Number = number;
                Unit = unit;
            }

            public string Name { get; }

            public string Text { get; }

            public bool IsNumeric { get; }

            public long Number { get; }

            public string Unit { get; }

            public static SampleLabel String(string name, string value) => new SampleLabel(name, value, false, 0, null);

            public static SampleLabel Numeric(string name, long number, string unit, string ignoredText = null)
            {
                return new SampleLabel(name, ignoredText, true, number, unit);
            }
        }
    }
}
