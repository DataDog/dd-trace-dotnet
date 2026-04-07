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
            throw new System.Exception("Something went wrong");
        }

        public string ThrowExceptionIndex()
        {
            throw new System.Exception("Invalid index Exception");
        }
    }
}
