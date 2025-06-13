// <copyright file="ProfileAllocations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using K4os.Compression.LZ4.Streams;
using Perftools.Profiles;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace AllocSimulator
{
    public class ProfileAllocations
    {
        private const string AllocationsCount = "alloc-samples";
        private const string AllocationsSize = "alloc-size";
        private const string AllocationClassLabel = "allocation class";

        private Profile _profile;
        private Dictionary<long, string> _stringTable;
        private List<string> _sampleTypes;
        private Dictionary<string, AllocInfo> _allocations;

        private ProfileAllocations(Profile profile)
        {
            _profile = profile;
        }

        private static readonly byte[] Lz4MagicNumber = BitConverter.GetBytes(0x184D2204);

        private static Stream GetStream(string filename)
        {
            var s = File.OpenRead(filename);
            var buffer = new byte[4];
            s.Read(buffer.AsSpan());
            s.Position = 0;
            if (Lz4MagicNumber.SequenceEqual(buffer))
            {
                return LZ4Stream.Decode(s);
            }
            else
            {
                return s;
            }
        }

        public static ProfileAllocations Load(string profileFilename)
        {
            using var s = GetStream(profileFilename);
            var profile = Profile.Parser.ParseFrom(s);

            var allocations = new ProfileAllocations(profile);

            allocations.Load();

            return allocations;
        }

        public IEnumerable<AllocInfo> GetAllocations()
        {
            return _allocations.Values;
        }

        public void Load()
        {
            LoadStringTable();
            LoadValueTypes();

            // get the sample offset corresponding to allocations count and size
            int countOffset = -1;
            int sizeOffset = -1;
            int currentOffset = 0;
            foreach (var type in _profile.SampleType)
            {
                if (GetString(type.Type) == AllocationsCount)
                {
                    countOffset = currentOffset;
                }
                else
                if (GetString(type.Type) == AllocationsSize)
                {
                    sizeOffset = currentOffset;
                }

                currentOffset++;
            }

            // iterate on all samples and keep only Allocations
            _allocations = new Dictionary<string, AllocInfo>(128);
            foreach (var sample in _profile.Sample)
            {
                var size = sample.Value[sizeOffset];
                if (size == 0)
                {
                    continue;
                }

                // look for the type name
                var labels = sample.Labels(_profile).ToArray();
                var type = labels.Single(l => l.Name == AllocationClassLabel).Value;

                if (!_allocations.TryGetValue(type, out var info))
                {
                    info = new AllocInfo()
                    {
                        Type = type,
                        Size = 0,
                        Count = 0
                    };

                    _allocations[type] = info;
                }

                info.Size += size;
                info.Count += (int)sample.Value[countOffset];
            }
        }

        public string GetString(long id)
        {
            if (_stringTable.TryGetValue(id, out var value))
            {
                return value;
            }

            return $"?{id}";
        }

        private void LoadStringTable()
        {
            var stringsCount = _profile.StringTable.Count;
            _stringTable = new Dictionary<long, string>(stringsCount);

            var current = 0;
            foreach (var entry in _profile.StringTable)
            {
                _stringTable[current] = entry;
                current++;
            }
        }

        private void LoadValueTypes()
        {
            _sampleTypes = new List<string>(_profile.SampleType.Count);
            foreach (var entry in _profile.SampleType)
            {
                _sampleTypes.Add(GetString(entry.Type));
            }
        }
    }

    internal struct Label
    {
        public string Name;
        public string Value;
    }

    internal static class Helpers
    {
        internal static IEnumerable<Label> Labels(this Perftools.Profiles.Sample sample, Profile profile)
        {
            return sample.Label.Select(
                label =>
                {
                    // support numeric labels
                    if ((label.Num != 0) || (label.NumUnit != 0))
                    {
                        return new Label { Name = profile.StringTable[(int)label.Key], Value = label.Num.ToString() };
                    }
                    else
                    {
                        // in case of 0 value and empty string, we return the latter
                        return new Label { Name = profile.StringTable[(int)label.Key], Value = profile.StringTable[(int)label.Str] };
                    }
                });
        }
    }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning restore SA1201 // Elements should appear in the correct order
#pragma warning restore SA1402 // File may only contain a single type
