using System;
using System.Collections.Generic;
using HotChocolate;

namespace Samples.HotChocolate
{
    public class Book
    {
        public string Title { get; set; }

        public Author Author { get; set; }
    }

    public class Author
    {
        public string Name { get; set; }
    }

    public class Query
    {
        public Book GetBook() =>
            new() { Title = "C# in depth.", Author = new Author { Name = "Jon Skeet" } };

        public string ThrowException()
        {
            try
            {
                throw new System.Exception("Something went wrong");
            }
            catch (Exception ex)
            {
                var error = ErrorBuilder
                           .New()
                           .SetMessage(ex.Message)
                           .SetCode("ERROR_CODE")
                           .SetException(ex)
                           .Build();

               var extensions = new Dictionary<string, object>
                {
                    { "int", 1 },
                    { "float", 1.1f },
                    { "str", "1" },
                    { "bool", true },
                    { "other", new object[] { 1, "foo" } },
                    { "sbyte", (sbyte)-42 },
                    { "byte", (byte)42 },
                    { "short", (short)-1000 },
                    { "ushort", (ushort)1000 },
                    { "uint", (uint)4294967295 },
                    { "long", (long)-9223372036854775808 },
                    { "ulong", (ulong)18446744073709551615 },
                    { "decimal", (decimal)3.1415926535897932384626433833 },
                    { "double", 3.1415926535897932384626433833 },
                    { "char", 'A' },
                    { "not_captured", "This should not be captured" }
                };

                throw new GraphQLException(error.WithExtensions(extensions));
            }
        }
    }
}
