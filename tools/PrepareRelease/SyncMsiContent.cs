using System;
using System.IO;
using System.Text;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;

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
        private const string Net461Property = "WIX_IS_NETFRAMEWORK_461_OR_LATER_INSTALLED";
        private const string Net461Condition = @"
        <Condition>" + Net461Property + "</Condition>";

        private static readonly string ItemTemplate = $@"
      <Component Win64=""$(var.Win64)"">{Net461Condition}
        <File Id=""{FileIdPrefixTemplate}{FileNameTemplate}""
              Source=""$(var.TracerHomeDirectory)\{FrameworkMonikerTemplate}\{FileNameTemplate}""
              KeyPath=""yes"" Checksum=""yes"" Assembly="".net""/>
      </Component>";

        private static readonly string FileTemplate = $@"<?xml version=""1.0"" encoding=""UTF-8""?>

<Wix xmlns=""http://schemas.microsoft.com/wix/2006/wi""
     xmlns:util=""http://schemas.microsoft.com/wix/UtilExtension"">
  <?include $(sys.CURRENTDIR)\Config.wxi?>
  <Fragment>
    <ComponentGroup Id=""{ComponentGroupIdTemplate}"" Directory=""{ComponentGroupDirectoryTemplate}"">{ComponentListTemplate}
    </ComponentGroup>
  </Fragment>
</Wix>
";

        private enum GacStatus
        {
            NotInGac = 0,
            Net45 = 1,
            Net461 = 2
        }

        public static void Run()
        {
            CreateWixFile(
                groupId: "Files.Managed.Net45.GAC",
                frameworkMoniker: "net45",
                groupDirectory: "net45.GAC",
                filePrefix: "net45_GAC_",
                GacStatus.Net45);
            CreateWixFile(
                groupId: "Files.Managed.Net461.GAC",
                frameworkMoniker: "net461",
                groupDirectory: "net461.GAC",
                filePrefix: "net461_GAC_",
                GacStatus.Net461);
            CreateWixFile(
                groupId: "Files.Managed.Net45",
                frameworkMoniker: "net45");
            CreateWixFile(
                groupId: "Files.Managed.Net461",
                frameworkMoniker: "net461");
            CreateWixFile(
                groupId: "Files.Managed.NetStandard20",
                frameworkMoniker: "netstandard2.0");
            CreateWixFile(
                groupId: "Files.Managed.Netcoreapp31",
                frameworkMoniker: "netcoreapp3.1");
        }

        private static void CreateWixFile(
            string groupId,
            string frameworkMoniker,
            string groupDirectory = null,
            string filePrefix = null,
            GacStatus gac = GacStatus.NotInGac)
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

            var filePaths = DependencyHelpers.GetTracerBinContent(frameworkMoniker);

            var components = string.Empty;

            foreach (var filePath in filePaths)
            {
                var fileName = Path.GetFileName(filePath);
                var component =
                        ItemTemplate
                           .Replace(FileIdPrefixTemplate, filePrefix)
                           .Replace(FrameworkMonikerTemplate, frameworkMoniker)
                           .Replace(FileNameTemplate, fileName);

                if (gac == GacStatus.NotInGac)
                {
                    component = component.Replace(@" Assembly="".net""", string.Empty);
                    component = component.Replace(Net461Condition, string.Empty);
                }
                else if (gac == GacStatus.Net45)
                {
                    component = component.Replace(Net461Property, $"NOT {Net461Property}");
                }

                components += component;
            }

            var wixFileContent =
                FileTemplate
                   .Replace(ComponentGroupDirectoryTemplate, groupDirectory)
                   .Replace(ComponentGroupIdTemplate, groupId)
                   .Replace(ComponentListTemplate, components);

            var wixFilePath = Path.Combine(wixProjectRoot, groupId + ".wxs");

            File.WriteAllText(wixFilePath, wixFileContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Console.WriteLine($"{groupId} Group successfully created.");
        }
    }
}
