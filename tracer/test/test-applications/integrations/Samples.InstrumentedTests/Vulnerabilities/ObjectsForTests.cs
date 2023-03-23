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

public class VulnerabilityList
{
    public List<TestVulnerability> Vulnerabilities { get; set; }
}

public class TestVulnerability
{
    public string type { get; set; }

    public EvidenceForTest Evidence { get; set; }
}

public class EvidenceForTest
{
    public string Value { get; set; }
}
