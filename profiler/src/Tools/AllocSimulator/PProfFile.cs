// <copyright file="PProfFile.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using K4os.Compression.LZ4.Streams;
using Perftools.Profiles;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable SA1025 // Code should not contain multiple whitespace in a row
#pragma warning disable SA1507 // Code should not contain multiple blank lines in a row
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1009 // Closing parenthesis should be spaced correctly
#pragma warning disable SA1108 // Block statements should not contain embedded comments
#pragma warning disable SA1111 // Closing parenthesis should be on line of last parameter

public class PProfFile
{
    public PProfFile()
    {
    }

    public IEnumerable<Sample> Samples { get; private set; }
    public IReadOnlyList<ValueType> ValueTypes { get; private set; }
    public IEnumerable<Mapping> Mappings { get; private set; }
    public IEnumerable<Location> Locations { get; private set; }
    public IEnumerable<Function> Functions { get; private set; }
    public IEnumerable<string> StringTable { get; private set; }
    public long DurationNS { get; private set; }

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

    // Can be called only once
    public bool Load(string filename)
    {
        using var stream = GetStream(filename);
        var profile = Profile.Parser.ParseFrom(stream);

        return Load(profile);
    }

    // Can be called only once
    private bool Load(Profile profile)
    {
        if (_profile != null)
        {
            return false;
        }

        try
        {
            _profile = profile;

            DurationNS = profile.DurationNanos;

            LoadStringTable();
            LoadValueTypes();
            LoadMappings();
            LoadFunctions();
            LoadLocations();
            LoadSamples();
        }
        catch (Exception x)
        {
            throw new InvalidOperationException(x.Message, x);
        }

        return true;
    }

    public string GetString(long id)
    {
        if (_stringTable.Count < id)
        {
            return $"?{id}";
        }

        return _stringTable[(int)id];
    }

    public string GetString(ulong id)
    {
        return GetString((long)id);
    }

    public string GetValueName(int pos)
    {
        if (_valueTypes.Count <= pos)
        {
            return $"v#{pos}";
        }

        return _valueTypes[pos].Name;
    }

    public Mapping GetMapping(ulong id)
    {                                       // looks like a "no mapping" id
        if ((_mappings.Count < (int)id) || (id == 0))
        {
            return null;
        }

        return _mappings[(int)id - 1];
    }

    public Function GetFunction(int id)
    {
        if (_functions.Count < id)
        {
            return null;
        }

        return _functions[id - 1];
    }

    public string GetFunctionName(int id)
    {
        var function = GetFunction(id);
        return function?.Name;
    }

    public Location GetLocation(ulong location)
    {
        if (_locations.Count < (int)location)
        {
            return null;
        }

        return _locations[(int)location - 1];
    }

    private void LoadStringTable()
    {
        var stringsCount = _profile.StringTable.Count;
        _stringTable = new List<string>(stringsCount);

        var current = 0;
        foreach (var entry in _profile.StringTable)
        {
            _stringTable.Add(entry);
            current++;
        }

        StringTable = _stringTable;
    }

    private void LoadValueTypes()
    {
        _valueTypes = new List<ValueType>(_profile.SampleType.Count);
        foreach (var entry in _profile.SampleType)
        {
            _valueTypes.Add(new ValueType(GetString(entry.Type), GetString(entry.Unit)));
        }

        ValueTypes = _valueTypes;
    }

    private void LoadFunctions()
    {
        _functions = new List<Function>(_profile.Function.Count);
        foreach (var function in _profile.Function)
        {
            _functions.Add(new Function(function.Id, GetString(function.Name), GetString(function.Filename), function.StartLine));
        }

        Functions = _functions;
    }

    private void LoadMappings()
    {
        _mappings = new List<Mapping>(_profile.Mapping.Count);
        foreach (var mapping in _profile.Mapping)
        {
            _mappings.Add(new Mapping(mapping.Id, GetString(mapping.Filename), mapping.BuildId));
        }

        Mappings = _mappings;
    }

    private void LoadLocations()
    {
        _locations = new List<Location>(_profile.Location.Count);
        foreach (var entry in _profile.Location)
        {
            var framesCount = entry.Line.Count;
            var frames = new List<Frame>(framesCount);
            for (int i = 0; i < framesCount; i++)
            {
                var function = GetFunction((int)entry.Line[i].FunctionId);
                frames.Add(
                    new Frame(
                        entry.Line[i].FunctionId,
                        function.Name,
                        function.Filename,
                        function.StartLine,
                        i != (framesCount - 1)  // the last frame is not inlined
                        )
                    );
            }

            var filename = "?";
            var mapping = GetMapping(entry.MappingId);
            if (mapping != null)
            {
                filename = mapping.Filename;
            }

            _locations.Add(
                new Location(
                    entry.Id, entry.MappingId, entry.Address, filename, frames
                    )
                );
        }

        Locations = _locations;
    }

    private void LoadSamples()
    {
        var samplesCount = _profile.Sample.Count;
        _samples = new List<Sample>(samplesCount);

        foreach (var sample in _profile.Sample)
        {
            var values = new List<long>(sample.Value);
            var labels = new List<Label>(sample.Label.Count);
            foreach (var label in sample.Label)
            {
                if ((label.Num != 0) || (label.NumUnit != 0))
                {
                    labels.Add(new Label(GetString(label.Key), label.Num.ToString()));
                }
                else if (label.Str != 0)
                {
                    labels.Add(new Label(GetString(label.Key), GetString(label.Str)));
                }
                else // we don't know if it is an empty string or a 0 numeric value
                {
                    labels.Add(new Label(GetString(label.Key), $"'{GetString(label.Str)}' or 0"));
                }
            }

            var locations = new List<Location>(sample.LocationId.Count);
            foreach (var location in sample.LocationId)
            {
                locations.Add(GetLocation(location));
            }

            _samples.Add(new Sample(values, labels, locations));
        }

        Samples = _samples;
    }

    private Profile _profile;
    private List<string> _stringTable;
    private List<ValueType> _valueTypes;
    private List<Function> _functions;
    private List<Mapping> _mappings;
    private List<Location> _locations;
    private List<Sample> _samples;
}



public class ValueType
{
    public ValueType(string name, string unit)
    {
        Name = name;
        Unit = unit;
    }

    public string Name { get; }
    public string Unit { get; }
}

public class Mapping
{
    public Mapping(ulong id, string filename, long buildId)
    {
        Id = id;
        Filename = filename;
        BuildId = buildId;
    }

    public ulong Id { get; }
    public string Filename { get; }
    public long BuildId { get; }
}

public class Frame
{
    public Frame(ulong id, string name, string filePath, long line, bool isInlined)
    {
        Id = id;
        Name = name;
        IsInlined = isInlined;
        FilePath = filePath;
        Line = line;
    }

    public ulong Id { get; }
    public string Name { get; }
    public string FilePath { get; }
    public long Line { get; }
    public bool IsInlined { get; }
}

public class Location
{
    public Location(ulong id, ulong mappingId, ulong address, string filename, List<Frame> frames)
    {
        Id = id;
        MappingId = mappingId;
        Address = address;
        Filename = filename;
        Frames = frames;
    }

    public ulong Id { get; }
    public ulong MappingId { get; }
    public ulong Address { get; }
    public string Filename { get; }

    // only the last frame is not inlined
    public IEnumerable<Frame> Frames { get; }
}

public class Function
{
    public Function(ulong id, string name, string filename, long startLine)
    {
        Id = id;
        Name = name;
        Filename = filename;
        StartLine = startLine;
    }

    public ulong Id { get; }
    public string Name { get; }
    public string Filename { get; }
    public long StartLine { get; }
}


public class Label
{
    public Label(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; }
    public string Value { get; }
}


public class Sample
{
    public Sample(List<long> values, List<Label> labels, List<Location> locations)
    {
        Values = values;
        Labels = labels;
        Locations = locations;
    }

    public IReadOnlyList<long> Values { get; }
    public IEnumerable<Label> Labels { get; }
    public IEnumerable<Location> Locations { get; }
}

#pragma warning restore SA1111 // Closing parenthesis should be on line of last parameter
#pragma warning restore SA1108 // Block statements should not contain embedded comments
#pragma warning restore SA1009 // Closing parenthesis should be spaced correctly
#pragma warning restore SA1201 // Elements should appear in the correct order
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore SA1402 // File may only contain a single type
#pragma warning restore SA1507 // Code should not contain multiple blank lines in a row
#pragma warning restore SA1025 // Code should not contain multiple whitespace in a row
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
