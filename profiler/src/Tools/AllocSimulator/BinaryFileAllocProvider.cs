// <copyright file="BinaryFileAllocProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Xml;

#pragma warning disable SA1201 // Elements should appear in the correct order

namespace AllocSimulator
{
    public class BinaryFileAllocProvider : IAllocProvider
    {
        private string _filename;
        private List<AllocInfo> _allocations;
        private List<string> _stringTable;

        public BinaryFileAllocProvider(string filename)
        {
            _filename = filename;
            _allocations = new List<AllocInfo>(1000000);
            _stringTable = new List<string>(128);

            ReadAllocations(_filename);
        }

        public IEnumerable<AllocInfo> GetAllocations()
        {
            return _allocations;
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

            ParseAllocations(filename);
        }

        private bool ParseAllocations(string filename)
        {
            try
            {
                using (var fileStream = File.OpenRead(filename))
                {
                    using (var reader = new BinaryReader(fileStream))
                    {
                        int pos = 0;
                        ReadStringTable(fileStream, ref pos);
                        ReadAllocations(fileStream, ref pos);
                    }
                }
            }
            catch (Exception x)
            {
                Console.WriteLine($"Error reading {filename} - {x.Message}");
                return false;
            }

            return true;
        }

        private void ReadStringTable(FileStream fileStream, ref int pos)
        {
            int currentString = 0;
            byte[] stringBuffer = new byte[2048];  // expect type names less than 2048 characters long
            int currentChar = 0;  // = length of the string after \0 is read
            byte[] buffer = new byte[1];

            // read character by character until \0
            while (pos < fileStream.Length)
            {
                while (pos < fileStream.Length)
                {
                    fileStream.Read(buffer, 0, 1);
                    pos++;

                    stringBuffer[currentChar] = buffer[0];

                    if (buffer[0] == 0)
                    {
                        break;
                    }

                    // don't add the \0
                    currentChar++;
                }

                // an empty string is ending the string table
                if (currentChar == 0)
                {
                    break;
                }

                var s = Encoding.UTF8.GetString(stringBuffer, 0, currentChar);
                _stringTable.Add(s);
                currentString++;

                currentChar = 0;
            }

            // TODO: currentString have been read into the string table
        }

        private void ReadAllocations(FileStream fileStream, ref int pos)
        {
            // each allocation is stored as 2 x 32 bit values:
            // string id followed by size
            using (var reader = new BinaryReader(fileStream))
            {
                UInt64 current = 0; // for debugging
                while (pos < fileStream.Length)
                {
                    var id = reader.ReadInt32();
                    var size = reader.ReadInt32();
                    var allocInfo = new AllocInfo()
                    {
                        Key = 0,
                        Count = 1,
                        Size = size,
                        Type = _stringTable[id]
                    };

                    _allocations.Add(allocInfo);
                    current++;
                    pos += 8;  // skip the read 2 x 4 bytes
                }
            }
        }
    }
}

#pragma warning restore SA1201 // Elements should appear in the correct order
