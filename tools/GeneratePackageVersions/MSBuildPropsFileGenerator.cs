using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneratePackageVersions
{
    public class MSBuildPropsFileGenerator : FileGenerator
    {
        private const string HeaderConst =
@"<Project>
  <ItemGroup>";

        private const string FooterConst =
@"  </ItemGroup>
</Project>";

        private const string EntryFormat =
@"    <PackageVersionSample Include=""samples*\**\{0}.csproj"">
      <ApiVersion>{1}</ApiVersion>
    </PackageVersionSample>";

        public MSBuildPropsFileGenerator(string filename)
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
                FileStringBuilder.AppendLine(string.Format(EntryFormat, sampleProjectName, packageVersion));
            }
        }
    }
}
