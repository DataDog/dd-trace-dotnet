using System;
using System.IO;
using System.Text;
using Datadog.Trace.TestHelpers;
using PrepareRelease.Tools;

namespace PrepareRelease
{
    public class SyncMsiContent
    {
        private const string FileNameTemplate = @"{{file_name}}";
        private const string ComponentListTemplate = @"{{component_list}}";
        private const string ComponentGroupIdTemplate = @"{{component_group_id}}";
        private const string ComponentGroupDirectoryTemplate = @"{{component_group_directory}}";
        private const string FileIdPrefixTemplate = @"{{file_id_prefix}}";
        private const string FrameworkMonikerTemplate = @"{{framework_moniker}}";

        private static readonly string Gac45ItemTemplate = $@"
      <Component Win64=""$(var.Win64)"">
        <File Id=""{FileIdPrefixTemplate}{FileNameTemplate}""
              Source=""$(var.ManagedDllPath)\{FrameworkMonikerTemplate}\{FileNameTemplate}""
              KeyPath=""yes"" Checksum=""yes"" Assembly="".net""/>
      </Component>";

        private static readonly string Gac45FileTemplate = $@"<?xml version=""1.0"" encoding=""UTF-8""?>

<Wix xmlns=""http://schemas.microsoft.com/wix/2006/wi""
     xmlns:util=""http://schemas.microsoft.com/wix/UtilExtension"">
  <?include $(sys.CURRENTDIR)\Config.wxi?>
  <Fragment>
    <ComponentGroup Id=""{ComponentGroupIdTemplate}"" Directory=""{ComponentGroupDirectoryTemplate}"">{ComponentListTemplate}
    </ComponentGroup>
  </Fragment>
</Wix>
";

        public static void Run()
        {
            CreateWixFile(
                groupId: "Files.Managed.Net45.GAC",
                frameworkMoniker: "net45",
                groupDirectory: "net45.GAC",
                filePrefix: "net45_GAC_",
                isGac: true);
            CreateWixFile(
                groupId: "Files.Managed.Net45",
                frameworkMoniker: "net45");
            CreateWixFile(
                groupId: "Files.Managed.Net461",
                frameworkMoniker: "net461");
            CreateWixFile(
                groupId: "Files.Managed.NetStandard20",
                frameworkMoniker: "netstandard2.0");
        }

        private static void CreateWixFile(
            string groupId,
            string frameworkMoniker,
            string groupDirectory = null,
            string filePrefix = null,
            bool isGac = false)
        {
            Console.WriteLine($"Creating the {groupId} Group");

            groupDirectory = groupDirectory ?? $"{frameworkMoniker}";
            filePrefix = filePrefix ?? $"{frameworkMoniker.Replace(".", string.Empty)}_";

            var solutionDirectory = EnvironmentHelper.GetSolutionDirectory();

            var wixProjectRoot =
                Path.Combine(
                    solutionDirectory,
                    "deploy",
                    "Datadog.Trace.ClrProfiler.WindowsInstaller");

            var filePaths = DependencyHelpers.GetTracerReleaseBinContent(frameworkMoniker);

            var components = string.Empty;

            foreach (var filePath in filePaths)
            {
                var fileName = Path.GetFileName(filePath);
                var component =
                        Gac45ItemTemplate
                           .Replace(FileIdPrefixTemplate, filePrefix)
                           .Replace(FrameworkMonikerTemplate, frameworkMoniker)
                           .Replace(FileNameTemplate, fileName);

                if (!isGac)
                {
                    component = component.Replace(@" Assembly="".net""", string.Empty);
                }

                components += component;
            }

            var wixFileContent =
                Gac45FileTemplate
                   .Replace(ComponentGroupDirectoryTemplate, groupDirectory)
                   .Replace(ComponentGroupIdTemplate, groupId)
                   .Replace(ComponentListTemplate, components);

            var wixFilePath = Path.Combine(wixProjectRoot, groupId + ".wxs");

            File.WriteAllText(wixFilePath, wixFileContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Console.WriteLine($"{groupId} Group successfully created.");
        }
    }
}
