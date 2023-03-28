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

