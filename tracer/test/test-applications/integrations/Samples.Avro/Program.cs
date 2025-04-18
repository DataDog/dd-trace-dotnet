using System;
using System.Collections.Generic;
using System.IO;
using Avro;
using Avro.Generic;
using Avro.IO;
using Avro.Specific;

namespace Samples.Avro;

internal class Program
{
    private const string Usage = "Usage: Avro [Default|SpecificDatum|GenericDatum]";

    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(Usage);
        }

        // our sample data
        var outMessage = new Person
        {
            name = "Jane Doe", phoneNumbers = new List<PhoneNumber>
            {
                new() { number = "67890", type = PhoneType.WORK },
                new() { number = "54321", type = PhoneType.MOBILE }
            }
        };
        Person inMessage;

        // use a simpler schema for the generic example because it's a pain to fill otherwise
        var schemaGenericJson = @"
        {
            ""type"": ""record"",
            ""name"": ""Person"",
            ""fields"": [
                { ""name"": ""name"", ""type"": ""string"" },
                { ""name"": ""email"", ""type"": ""string"", ""default"": """" }
            ]
        }";
        var schemaGeneric = Schema.Parse(schemaGenericJson);

        // serialization
        using var ms = new MemoryStream();
        var encoder = new BinaryEncoder(ms);
        using (SampleHelpers.CreateScope("Ser"))
        {
            // datum and default writers have the same write signature, but they don't share a common interface
            if (args[0].Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                var writer = new SpecificDefaultWriter(outMessage.Schema);
                writer.Write(outMessage, encoder);
            }
            else if (args[0].Equals("SpecificDatum", StringComparison.OrdinalIgnoreCase))
            {
                var writer = new SpecificDatumWriter<Person>(outMessage.Schema);
                writer.Write(outMessage, encoder);
            }
            else if (args[0].Equals("GenericDatum", StringComparison.OrdinalIgnoreCase))
            {
                var record = new GenericRecord((RecordSchema)schemaGeneric);
                record.Add("name", "Jane");
                record.Add("email", "jane.doe@example.com");

                var writer = new GenericDatumWriter<GenericRecord>(schemaGeneric);
                writer.Write(record, encoder);
            }
            else
            {
                throw new ArgumentException(Usage);
            }
        }

        // deserialization
        ms.Seek(offset: 0, SeekOrigin.Begin); // reset the cursor position since we're going to read from it now
        // TODO: would be nice to use a different but compatible schema for reception to make sure we capture the right schema
        var decoder = new BinaryDecoder(ms);
        using (SampleHelpers.CreateScope("Deser"))
        {
            if (args[0].Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                var reader = new SpecificDefaultReader(outMessage.Schema, outMessage.Schema);
                inMessage = reader.Read<Person>(reuse: null, decoder);
            }
            else if (args[0].Equals("SpecificDatum", StringComparison.OrdinalIgnoreCase))
            {
                var reader = new SpecificDatumReader<Person>(outMessage.Schema, outMessage.Schema);
                inMessage = reader.Read(reuse: null, decoder);
            }
            else if (args[0].Equals("GenericDatum", StringComparison.OrdinalIgnoreCase))
            {
                var reader = new GenericDatumReader<GenericRecord>(schemaGeneric, schemaGeneric);
                var genericMessage = reader.Read(reuse: null, decoder);
                Console.WriteLine(genericMessage.ToString());
                inMessage = null;
            }
            else
            {
                throw new ArgumentException(Usage);
            }
        }

        Console.WriteLine(inMessage);
    }
}
