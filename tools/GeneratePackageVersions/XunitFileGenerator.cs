using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneratePackageVersions
{
    public class XUnitFileGenerator : FileGenerator
    {
        private const string HeaderConst =
@"using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class PackageVersions
    {";

        private const string FooterConst =
@"    }
}";

        private const string EntryFormat =
@"                new object[] {{ ""{0}"" }},";

        private const string BodyFormat =
@"        public static IEnumerable<object[]> {0} =>

            new List<object[]>
            {{
#if VS_COMPILE
                new object[] {{ string.Empty }},
#else{1}
#endif
            }};";

        public XUnitFileGenerator(string filename)
            : base(filename)
        {
        }

        protected override string Header
        {
            get
            {
                return HeaderConst;
            }
        }

        protected override string Footer
        {
            get
            {
                return FooterConst;
            }
        }

        public override void Write(string integrationName, string sampleProjectName, IEnumerable<string> packageVersions)
        {
            Debug.Assert(Started, "Cannot call Write() before calling Start()");
            Debug.Assert(!Finished, "Cannot call Write() after calling Finish()");

            StringBuilder bodyStringBuilder = new StringBuilder();
            foreach (string packageVersion in packageVersions)
            {
                bodyStringBuilder.AppendLine();
                bodyStringBuilder.Append(string.Format(EntryFormat, packageVersion));
            }

            FileStringBuilder.AppendLine(string.Format(BodyFormat, integrationName, bodyStringBuilder.ToString()));
        }
    }
}
