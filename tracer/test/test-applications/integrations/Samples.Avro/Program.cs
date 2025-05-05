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

    // use a simpler schema for the generic example because it's a pain to fill otherwise
    private const string SchemaGenericJson = @"
        {
            ""type"": ""record"",
            ""name"": ""Person"",
            ""fields"": [
                { ""name"": ""name"", ""type"": ""string"" },
                { ""name"": ""email"", ""type"": ""string"", ""default"": """" }
            ]
        }";

    private static readonly Schema SchemaGeneric = Schema.Parse(SchemaGenericJson);

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

        // Instrumented methods need to be called from sub-methods.
        // If they are called from Main directly, we cannot prevent inlining, which prevents instrumentation to kick-in in net5.0 for this particular case
        using var ms = Serialize(args[0], outMessage);
        Deserialize(args[0], ms, outMessage.Schema);
    }

    /// <param name="type">select which serializer we use, see usage</param>
    /// <param name="outMessage">the message to write, ignored when in "generic" mode</param>
    private static MemoryStream Serialize(string type, Person outMessage)
    {
        MemoryStream ms = null;
        try
        {
            ms = new MemoryStream();
            var encoder = new BinaryEncoder(ms);
            using (SampleHelpers.CreateScope("Ser"))
            {
                // datum and default writers have the same write signature, but they don't share a common interface
                if (type.Equals("Default", StringComparison.OrdinalIgnoreCase))
                {
                    var writer = new SpecificDefaultWriter(outMessage.Schema);
                    writer.Write(outMessage, encoder);
                }
                else if (type.Equals("SpecificDatum", StringComparison.OrdinalIgnoreCase))
                {
                    var writer = new SpecificDatumWriter<Person>(outMessage.Schema);
                    writer.Write(outMessage, encoder);
                }
                else if (type.Equals("GenericDatum", StringComparison.OrdinalIgnoreCase))
                {
                    var record = new GenericRecord((RecordSchema)SchemaGeneric);
                    record.Add("name", "Jane");
                    record.Add("email", "jane.doe@example.com");

                    var writer = new GenericDatumWriter<GenericRecord>(SchemaGeneric);
                    writer.Write(record, encoder);
                }
                else
                {
                    throw new ArgumentException(Usage);
                }
            }

            return ms;
        }
        catch
        {
            ms?.Dispose();
            throw;
        }
    }

    /// <param name="type">select which deserializer we use, see usage</param>
    /// <param name="schema">the schema to read with, ignored when in "generic" mode</param>
    private static void Deserialize(string type, MemoryStream ms, Schema schema)
    {
        Person inMessage;
        ms.Seek(offset: 0, SeekOrigin.Begin); // reset the cursor position since we're going to read from it now
        // TODO: would be nice to use a different but compatible schema for reception to make sure we capture the right schema
        var decoder = new BinaryDecoder(ms);
        using (SampleHelpers.CreateScope("Deser"))
        {
            if (type.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                var reader = new SpecificDefaultReader(schema, schema);
                inMessage = reader.Read<Person>(reuse: null, decoder);
            }
            else if (type.Equals("SpecificDatum", StringComparison.OrdinalIgnoreCase))
            {
                var reader = new SpecificDatumReader<Person>(schema, schema);
                inMessage = reader.Read(reuse: null, decoder);
            }
            else if (type.Equals("GenericDatum", StringComparison.OrdinalIgnoreCase))
            {
                var reader = new GenericDatumReader<GenericRecord>(SchemaGeneric, SchemaGeneric);
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
