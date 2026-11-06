// <copyright file="PprofProfile.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Datadog.Profiler.IntegrationTests.Helpers;
using Perftools.Profiles;
using PprofSample = Perftools.Profiles.Sample;

namespace Datadog.Profiler.IntegrationTests.Helpers.Profiles
{
    internal sealed class PprofProfile
    {
        private PprofProfile(string path, DateTime timestampUtc, IReadOnlyList<ProfileValue> sampleTypes, IReadOnlyList<ProfileSample> samples)
        {
            Path = path;
            TimestampUtc = timestampUtc;
            SampleTypes = sampleTypes;
            Samples = samples;
        }

        public string Path { get; }

        public DateTime TimestampUtc { get; }

        public IReadOnlyList<ProfileValue> SampleTypes { get; }

        public IReadOnlyList<ProfileSample> Samples { get; }

        internal static PprofProfile Create(string path, DateTime timestampUtc, Profile profile)
        {
            var origin = "'" + path + "' (" + timestampUtc.ToString("o", CultureInfo.InvariantCulture) + ")";
            var sampleTypes = ReadSampleTypes(profile, path);
            var locations = IndexById(profile.Location, path, "location");
            var functions = IndexById(profile.Function, path, "function");

            var samples = new ProfileSample[profile.Sample.Count];
            for (var i = 0; i < profile.Sample.Count; i++)
            {
                samples[i] = ReadSample(profile, profile.Sample[i], origin, sampleTypes, locations, functions, path);
            }

            return new PprofProfile(path, timestampUtc, sampleTypes, samples);
        }

        private static ProfileValue[] ReadSampleTypes(Profile profile, string path)
        {
            var sampleTypes = new ProfileValue[profile.SampleType.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < profile.SampleType.Count; i++)
            {
                var sampleType = profile.SampleType[i];
                var name = StringAt(profile, sampleType.Type, path, "sample type");
                var unit = StringAt(profile, sampleType.Unit, path, "sample unit");
                if (!seen.Add(name))
                {
                    throw new InvalidDataException("Profile '" + path + "' has duplicate sample type '" + name + "'.");
                }

                sampleTypes[i] = ProfileValue.FromSampleType(name, unit);
            }

            return sampleTypes;
        }

        private static ProfileSample ReadSample(
            Profile profile,
            PprofSample sample,
            string origin,
            ProfileValue[] sampleTypes,
            Dictionary<ulong, Location> locations,
            Dictionary<ulong, Function> functions,
            string path)
        {
            var present = new bool[sampleTypes.Length];
            var amounts = new long[sampleTypes.Length];
            var valueCount = Math.Min(sample.Value.Count, sampleTypes.Length);
            for (var i = 0; i < valueCount; i++)
            {
                present[i] = true;
                amounts[i] = sample.Value[i];
            }

            var labels = new ProfileLabel[sample.Label.Count];
            for (var i = 0; i < sample.Label.Count; i++)
            {
                labels[i] = ReadLabel(profile, sample.Label[i], path);
            }

            var frames = new StackFrame[sample.LocationId.Count];
            for (var i = 0; i < sample.LocationId.Count; i++)
            {
                frames[i] = ReadFrame(profile, sample.LocationId[i], locations, functions, path);
            }

            return new ProfileSample(
                new ProfileValues(origin, sampleTypes, present, amounts),
                new ProfileLabels(origin, labels),
                new StackTrace(frames));
        }

        private static ProfileLabel ReadLabel(Profile profile, Label label, string path)
        {
            var name = StringAt(profile, label.Key, path, "label");

            // Numeric when num or num_unit is set, including a numeric zero. Same rule as PprofHelper.Labels.
            if (label.Num != 0 || label.NumUnit != 0)
            {
                var unit = label.NumUnit == 0 ? string.Empty : StringAt(profile, label.NumUnit, path, "label unit");
                return ProfileLabel.Numeric(name, label.Num, unit);
            }

            return ProfileLabel.Text(name, StringAt(profile, label.Str, path, "label value"));
        }

        private static StackFrame ReadFrame(
            Profile profile,
            ulong locationId,
            Dictionary<ulong, Location> locations,
            Dictionary<ulong, Function> functions,
            string path)
        {
            if (!locations.TryGetValue(locationId, out var location))
            {
                throw new InvalidDataException("Profile '" + path + "' sample references missing location " + locationId + ".");
            }

            if (location.Line.Count == 0)
            {
                throw new InvalidDataException("Profile '" + path + "' location " + locationId + " has no line.");
            }

            // Inlined frames are extra lines on one location. The legacy reader keeps the first line only.
            var line = location.Line[0];
            if (!functions.TryGetValue(line.FunctionId, out var function))
            {
                throw new InvalidDataException("Profile '" + path + "' location " + locationId + " references missing function " + line.FunctionId + ".");
            }

            var frame = StringAt(profile, function.Name, path, "function");
            var filename = StringAt(profile, function.Filename, path, "filename");
            return new StackFrame(frame, filename, line.Line_);
        }

        private static Dictionary<ulong, T> IndexById<T>(IEnumerable<T> items, string path, string kind)
            where T : class
        {
            var index = new Dictionary<ulong, T>();
            foreach (var item in items)
            {
                var id = IdOf(item);
                if (!index.TryAdd(id, item))
                {
                    throw new InvalidDataException("Profile '" + path + "' has duplicate " + kind + " id " + id + ".");
                }
            }

            return index;
        }

        private static ulong IdOf<T>(T item)
        {
            if (item is Location location)
            {
                return location.Id;
            }

            if (item is Function function)
            {
                return function.Id;
            }

            throw new InvalidOperationException("Cannot index " + typeof(T).Name + " by id.");
        }

        private static string StringAt(Profile profile, long index, string path, string field)
        {
            if (index < 0 || index >= profile.StringTable.Count)
            {
                throw new InvalidDataException(
                    "Profile '" + path + "' has " + field + " string index " + index + " outside the string table (" + profile.StringTable.Count + " entries).");
            }

            return profile.StringTable[(int)index];
        }
    }
}
