// <copyright file="SyncMsiContent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Text;

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

        public static void Run(string tracerDirectory, string outputDirectory)
        {
            CreateWixFile(
                tracerDirectory,
                outputDirectory,
                groupId: "Files.Managed.Net45.GAC",
                frameworkMoniker: "net45",
                groupDirectory: "net45.GAC",
                filePrefix: "net45_GAC_",
                GacStatus.Net45);
            CreateWixFile(
                tracerDirectory,
                outputDirectory,
                groupId: "Files.Managed.Net461.GAC",
                frameworkMoniker: "net461",
                groupDirectory: "net461.GAC",
                filePrefix: "net461_GAC_",
                GacStatus.Net461);
            CreateWixFile(
                tracerDirectory,
                outputDirectory,
                groupId: "Files.Managed.Net45",
                frameworkMoniker: "net45");
            CreateWixFile(
                tracerDirectory,
                outputDirectory,
                groupId: "Files.Managed.Net461",
                frameworkMoniker: "net461");
            CreateWixFile(
                tracerDirectory,
                outputDirectory,
                groupId: "Files.Managed.NetStandard20",
                frameworkMoniker: "netstandard2.0");
            CreateWixFile(
                tracerDirectory,
                outputDirectory,
                groupId: "Files.Managed.Netcoreapp31",
                frameworkMoniker: "netcoreapp3.1");
        }

        private static void CreateWixFile(
            string tracerDirectory,
            string outputDirectory,
            string groupId,
            string frameworkMoniker,
            string groupDirectory = null,
            string filePrefix = null,
            GacStatus gac = GacStatus.NotInGac)
        {
            Console.WriteLine($"Creating the {groupId} Group");

            groupDirectory ??= $"{frameworkMoniker}";
            filePrefix ??= $"{frameworkMoniker.Replace(".", string.Empty)}_";

            var wixProjectRoot =
                Path.Combine(
                    tracerDirectory,
                    "src",
                    "WindowsInstaller");

            var extensions = gac == GacStatus.NotInGac ? new[] { ".dll", ".pdb" } : new[] { ".dll" };

            var filePaths = GetTracerBinContent(outputDirectory, frameworkMoniker, extensions);

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

        private static string[] GetTracerBinContent(string outputDirectory, string frameworkMoniker, string[] extensions)
        {
            var outputFolder = Path.Combine(outputDirectory, frameworkMoniker);

            var filePaths = Directory.EnumerateFiles(
                                          outputFolder,
                                          "*.*",
                                          SearchOption.AllDirectories)
                                     .Where(f => extensions.Contains(Path.GetExtension(f)))
                                     .ToArray();

            if (filePaths.Length == 0)
            {
                throw new Exception("Be sure to build in release mode before running this tool.");
            }

            return filePaths;
        }
    }
}
