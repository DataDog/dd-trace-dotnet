using System;
using System.IO;
using Avro.IO;
using Avro.Specific;

namespace Samples.Avro;

internal class Program
{
    private static void Main()
    {
        // our sample data
        var outMessage = new Person { name = "Jane Doe", phoneNumbers = { new PhoneNumber { number = "67890", type = PhoneType.WORK }, new PhoneNumber { number = "54321", type = PhoneType.MOBILE } } };
        Person inMessage;

        // serialization
        using var ms = new MemoryStream();
        var encoder = new BinaryEncoder(ms);
        var writer = new SpecificDefaultWriter(outMessage.Schema);
        using (SampleHelpers.CreateScope("Ser"))
        {
            writer.Write(outMessage, encoder);
        }

        // deserialization
        ms.Seek(offset: 0, SeekOrigin.Begin); // reset the cursor position since we're going to read from it now
        // TODO: would be nice to use a different but compatible schema for reception to make sure we capture the right schema
        var reader = new SpecificDatumReader<Person>(outMessage.Schema, outMessage.Schema);
        var decoder = new BinaryDecoder(ms);
        using (SampleHelpers.CreateScope("Deser"))
        {
            inMessage = reader.Read(reuse: null, decoder);
        }

        Console.WriteLine(inMessage.ToString());
    }
}
