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
                    { "float", 1.1 },
                    { "str", "1" },
                    { "bool", true },
                    { "other", new object[] { 1, "foo" } },
                    { "not_captured", "This should not be captured" }
                };

                throw new GraphQLException(error.WithExtensions(extensions));
            }
        }
    }
}
