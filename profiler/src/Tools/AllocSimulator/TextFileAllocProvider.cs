// <copyright file="TextFileAllocProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable SA1513 // Closing brace should be followed by blank line

namespace AllocSimulator
{
    public class TextFileAllocProvider : IAllocProvider
    {
        private List<LoopAllocs> _allocations;
        private string _filename;

        public TextFileAllocProvider(string filename)
        {
            _filename = filename;
            ReadAllocations(_filename);
        }

        public IEnumerable<AllocInfo> GetAllocations()
        {
            Random r = new Random(DateTime.Now.Millisecond);

            // execute the LoopAllocs
            foreach (var alloc in _allocations)
            {
                var allocationsCount = alloc.Allocations.Count;
                if (allocationsCount == 1)
                {
                    yield return alloc.Allocations[0];
                }
                else
                {
                    for (int i = 0; i < alloc.Iterations; i++)
                    {
                        if (alloc.IsRandom)
                        {
                            int index = r.Next(allocationsCount);
                            yield return alloc.Allocations[index];
                        }
                        else
                        {
                            foreach (var current in alloc.Allocations)
                            {
                                yield return current;
                            }
                        }
                    }
                }
            }
        }

        private static AllocInfo GetAllocInfo(string line, int currentLine)
        {
            // type,key,count,size
            // Object2,1,1,2
            var alloc = new AllocInfo();
            var text = line.AsSpan();

            // get type
            int pos = text.IndexOf(',');
            if (pos == -1)
            {
                throw new FormatException($"Invalid allocation definition: '{line}' in line {currentLine}");
            }
            alloc.Type = text.Slice(0, pos).ToString();
            pos++;

            // get key
            text = line.AsSpan(pos);
            var next = text.IndexOf(',');
            if (next == -1)
            {
                throw new FormatException($"Invalid allocation definition: '{line}' in line {currentLine}");
            }
            text = line.AsSpan(pos, next);
            alloc.Key = int.Parse(text);
            pos += next + 1;

            // get count
            text = line.AsSpan(pos);
            next = text.IndexOf(',');
            if (next == -1)
            {
                throw new FormatException($"Invalid allocation definition: '{line}' in line {currentLine}");
            }
            text = line.AsSpan(pos, next);
            alloc.Count = int.Parse(text);
            pos += next + 1;

            // get size
            text = line.AsSpan(pos);
            alloc.Size = int.Parse(text);

            return alloc;
        }

        private static int GetLoopIterations(string line, int currentLine)
        {
            ReadOnlySpan<char> countString = line.AsSpan(2);
            if (!int.TryParse(countString, out var count))
            {
                throw new ArgumentOutOfRangeException($"Invalid iterations '{countString}' in line {currentLine}");
            }

            return count;
        }

        private void ReadAllocations(string filename)
        {
            if (filename == null)
            {
                throw new ArgumentNullException(nameof(filename));
            }

            if (!File.Exists(filename))
            {
                throw new FileNotFoundException("Missing allocations file", filename);
            }

            _allocations = new List<LoopAllocs>();
            ParseAllocations(filename);
        }

        private bool ParseAllocations(string filename)
        {
            int currentLine = 1;
            ReaderState state = ReaderState.None;
            LoopAllocs allocs = null;

            try
            {
                foreach (string line in File.ReadLines(filename))
                {
                    // skip empty lines and comments
                    if (line == string.Empty || line.StartsWith("//"))
                    {
                        currentLine++;
                        continue;
                    }

                    switch (state)
                    {
                        case ReaderState.None:
                            {
                                if (line.StartsWith("*="))
                                {
                                    int iterations = GetLoopIterations(line, currentLine);
                                    allocs = new LoopAllocs();
                                    allocs.Iterations = iterations;

                                    currentLine++;
                                    state = ReaderState.Loop;
                                }
                                else
                                if (line.StartsWith("?="))
                                {
                                    int iterations = GetLoopIterations(line, currentLine);
                                    allocs = new LoopAllocs();
                                    allocs.Iterations = iterations;
                                    allocs.IsRandom = true;

                                    currentLine++;
                                    state = ReaderState.RandomLoop;
                                }
                                else
                                {
                                    // should be a single allocation action
                                    var alloc = GetAllocInfo(line, currentLine);
                                    allocs = new LoopAllocs();
                                    allocs.Iterations = 1;
                                    allocs.Allocations.Add(alloc);
                                    _allocations.Add(allocs);
                                    allocs = null;

                                    currentLine++;
                                }

                                break;
                            }

                        case ReaderState.Loop:
                            {
                                // check for end of loop
                                if (line == "*")
                                {
                                    _allocations.Add(allocs);
                                    allocs = null;
                                    state = ReaderState.None;
                                }
                                else
                                {
                                    // should be a single allocation action
                                    var alloc = GetAllocInfo(line, currentLine);
                                    allocs.Allocations.Add(alloc);
                                }

                                currentLine++;
                                break;
                            }

                        case ReaderState.RandomLoop:
                            {
                                // check for end of loop
                                if (line == "?")
                                {
                                    _allocations.Add(allocs);
                                    allocs = null;
                                    state = ReaderState.None;
                                }
                                else
                                {
                                    // should be a single allocation action
                                    var alloc = GetAllocInfo(line, currentLine);
                                    allocs.Allocations.Add(alloc);
                                }

                                currentLine++;
                                break;
                            }
                    }
                }
            }
            catch (Exception x)
            {
                Console.WriteLine($"Error on line {currentLine}: {x.GetType()} - {x.Message}");
                return false;
            }

            return true;
        }

        private enum ReaderState
        {
            None = 0,
            Loop,
            RandomLoop
        }
    }
}

#pragma warning restore SA1513 // Closing brace should be followed by blank line
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore SA1201 // Elements should appear in the correct order
#pragma warning restore CS8601 // Possible null reference assignment.
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
