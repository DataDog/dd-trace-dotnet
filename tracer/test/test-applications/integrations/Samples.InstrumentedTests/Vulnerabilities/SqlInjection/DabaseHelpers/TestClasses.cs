using System;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class BookSerialize
{
    public BookSerialize()
    { }
    public Guid Id { get; set; }
    public string Title { get; set; }

    public string a { get; set; }

    public string b { get; set; }

    public int i { get; set; }

    public string Author { get; set; } = "Default";
}

public class Book
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
}
