// <copyright file="LibraryService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Demos.NoOpWcfService.Library
{
    public class LibraryService : ILibraryService
    {
        private List<Book> _books;

        public LibraryService()
        {
            _books = new List<Book>();
            for (int i = 0; i < 20; ++i)
            {
                _books.Add(new Book { ID = i, Name = "Name " + i });
            }
        }

        public Book SearchBook(string bookName)
        {
            return _books.Find(w => w.Name == bookName);
        }
    }
}
