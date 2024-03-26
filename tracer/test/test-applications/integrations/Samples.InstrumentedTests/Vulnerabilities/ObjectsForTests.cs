using System;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;
struct StructForStringTest
{
    readonly string str;
    public StructForStringTest(string str)
    {
        this.str = str;
    }
    public override string ToString()
    {
        return str;
    }
}

class ClassForStringTest
{
    readonly string str;
    public ClassForStringTest(string str)
    {
        this.str = str;
    }
    public override string ToString()
    {
        return str;
    }
}

public class FormatProviderForTest : IFormatProvider, ICustomFormatter
{
    public string Format(string format, object arg, IFormatProvider formatProvider)
    {
        return (arg.ToString() + "customformat");
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

public class CustomerNumberFormatter : IFormatProvider, ICustomFormatter
{
    public object GetFormat(Type formatType)
    {
        if (formatType == typeof(ICustomFormatter))
        {
            return this;
        }
        return null;
    }

    public string Format(string format, object arg, IFormatProvider provider)
    {
        if (arg is Int32)
        {
            string custNumber = ((int)arg).ToString("D10");
            return custNumber.Substring(0, 4) + "-" + custNumber.Substring(4, 3) + "-" + custNumber.Substring(7, 3);
        }
        else
        {
            return null;
        }
    }
}

public class Customer
{
    private int custNumber;
    public Customer Parent { get; set; }
    public Customer Child { get; set; }

    public Customer() { }

    public Customer(string name, int number)
    {
        this.Name = name;
        this.custNumber = number;
    }

    public string Name { get; set; }

    public int CustomerNumber
    {
        get { return this.custNumber; }
        set { this.custNumber = value; }
    }
}
