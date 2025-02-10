using System;
using Google.Protobuf;
using Sample;
using static Sample.Person.Types;

namespace Samples.GoogleProtobuf;

/// <summary>
/// If this is all red it's because you haven't compiled the project yet.
/// The C# code for the protobuf object is generated as a pre-build event
/// to ensure that it's consistent with the version of protobuf used (useful for testing older versions)
/// </summary>
internal class Program
{
    private static void Main()
    {
        // sample data
        var john = new Person { Name = "John Doe", Email = "john@doe.com", Phones = { new PhoneNumber { Number = "12345", Type = PhoneType.Home } } };
        var jane = new Person { Name = "Jane Doe", Email = "jane@doe.com", Phones = { new PhoneNumber { Number = "67890", Type = PhoneType.Work }, new PhoneNumber { Number = "54321", Type = PhoneType.Mobile } } };
        var addressBook = new AddressBook { People = { { 12, john }, { 21, jane } } };

        // serialization

        byte[] serialized;
        using (SampleHelpers.CreateScope("Ser"))
        {
            serialized = addressBook.ToByteArray();
        }

        // deserialization
        var deserialized = new AddressBook();
        using (SampleHelpers.CreateScope("Deser"))
        {
            deserialized.MergeFrom(serialized);
        }

        Console.WriteLine(deserialized.People.Count);
    }
}
