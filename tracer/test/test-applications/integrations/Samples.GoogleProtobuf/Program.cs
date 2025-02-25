using System;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Sample;
using static Sample.Person.Types;

namespace Samples.GoogleProtobuf;

internal class Program
{
    private const string Usage = "Usage: GoogleProtobuf.exe [AddressBook|TimeStamp]";

    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException(Usage);
        }

        IMessage outMessage;
        IMessage inMessage;
        if (args[0].Equals("TimeStamp", StringComparison.OrdinalIgnoreCase))
        {
            // use a raw google protobuf type (shouldn't be instrumented)
            outMessage = Timestamp.FromDateTime(DateTime.UtcNow);
            inMessage = new Timestamp();
        }
        else if (args[0].Equals("AddressBook", StringComparison.OrdinalIgnoreCase))
        {
            // our own sample data
            var john = new Person { Name = "John Doe", Email = "john@doe.com", Phones = { new PhoneNumber { Number = "12345", Type = PhoneType.Home } } };
            var jane = new Person { Name = "Jane Doe", Email = "jane@doe.com", Phones = { new PhoneNumber { Number = "67890", Type = PhoneType.Work }, new PhoneNumber { Number = "54321", Type = PhoneType.Mobile } } };
            outMessage = new AddressBook { People = { { 12, john }, { 21, jane } } };
            inMessage = new AddressBook();
        }
        else
        {
            throw new ArgumentException(Usage);
        }

        // serialization
        byte[] serialized;
        using (SampleHelpers.CreateScope("Ser"))
        {
            serialized = outMessage.ToByteArray();
        }

        // deserialization
        using (SampleHelpers.CreateScope("Deser"))
        {
            inMessage.MergeFrom(serialized);
        }

        Console.WriteLine(inMessage.ToString());
    }
}
