using System;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;
public class FormatProviderForTest : IFormatProvider, ICustomFormatter
{
    public string Format(string format, object arg, IFormatProvider formatProvider)
    {
        return (arg.ToString());
    }

    public object GetFormat(Type formatType)
    {
        if (formatType == typeof(ICustomFormatter))
        {
            return this;
        }
        else
        {
            return null;
        }
    }
}

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
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
}
