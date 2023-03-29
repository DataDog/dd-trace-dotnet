using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.StringPropagation;

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
