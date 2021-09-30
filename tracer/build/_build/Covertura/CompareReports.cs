// <copyright file="CompareReports.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Nuke.Common.IO;

namespace Covertura
{
    public static class CodeCoverage
    {
        // somewhat arbitrary, but used to exclude noise
        const decimal SignificantChangeThreshold = 0.05m;
        // These classes change from run to run, so don't list them in expected changes
        static readonly string[] IgnoredClassPrefixes =
        {
            "Datadog.Trace.Logging.LogRateLimiter",
            "Datadog.Trace.RuntimeMetrics.",
            "Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations.TaskContinuationGenerator",
        };

        public static CoverturaReport ReadReport(AbsolutePath path)
        {
            var doc = XDocument.Load(path);
            var report = new CoverturaReport();

            // get coverage element
            var coverage = doc.Descendants("coverage").First();

            report.LineRate = decimal.Parse(coverage.Attribute("line-rate").Value);
            report.BranchRate = decimal.Parse(coverage.Attribute("branch-rate").Value);
            report.LinesCovered = int.Parse(coverage.Attribute("lines-covered").Value);
            report.LinesValid = int.Parse(coverage.Attribute("lines-valid").Value);
            report.BranchesCovered = int.Parse(coverage.Attribute("branches-covered").Value);
            report.BranchesValid = int.Parse(coverage.Attribute("branches-valid").Value);
            report.Complexity = int.Parse(coverage.Attribute("complexity").Value);

            report.Packages = coverage
                             .Descendants("packages")
                             .SelectMany(x => x.Descendants("package"))
                             .Select(packageEle => new CoverturaReport.Package
                              {
                                  Name = packageEle.Attribute("name").Value,
                                  LineRate = decimal.Parse(packageEle.Attribute("line-rate").Value),
                                  BranchRate = decimal.Parse(packageEle.Attribute("branch-rate").Value),
                                  Complexity = int.Parse(packageEle.Attribute("complexity").Value),
                                  Classes = packageEle
                                           .Descendants("classes")
                                           .SelectMany(x => x.Descendants("class"))
                                           .Select(classEle =>
                                            {
                                                var lineHits = classEle.Descendants("lines")
                                                                       .First()
                                                                       .Descendants("line")
                                                                       .Select(x => int.Parse(x.Attribute("hits").Value) > 0)
                                                                       .ToList();

                                                return new CoverturaReport.ClassDetails()
                                                {
                                                    Name = classEle.Attribute("name").Value,
                                                    Filename = classEle.Attribute("filename").Value,
                                                    LineRate = decimal.Parse(classEle.Attribute("line-rate").Value),
                                                    BranchRate = decimal.Parse(classEle.Attribute("branch-rate").Value),
                                                    Complexity = int.Parse(classEle.Attribute("complexity").Value),
                                                    LinesCovered = lineHits.Count,
                                                    LinesValid = lineHits.Count(x => x),
                                                };
                                            })
                                           .ToDictionary(x => $"{x.Filename}_{x.Name}", x => x),
                              })
                             .ToDictionary(x => x.Name, x => x);

            return report;
        }

        public static CoverturaReportComparison Compare(CoverturaReport oldReport, CoverturaReport newReport)
        {
            var comparison = new CoverturaReportComparison
            {
                Old = oldReport,
                New = newReport,
                LineCoverageChange = decimal.Round(newReport.LineRate - oldReport.LineRate, decimals: 2),
                BranchCoverageChange = decimal.Round(newReport.BranchRate - oldReport.BranchRate, decimals: 2),
                ComplexityChange = newReport.Complexity - oldReport.Complexity,
            };

            var matchedPackages = new Dictionary<string, CoverturaReportComparison.PackageChanges>();

            foreach (var kvp in oldReport.Packages)
            {
                var oldPackage = kvp.Value;
                // try and find package in other report
                if (!newReport.Packages.TryGetValue(kvp.Key, out var newPackage))
                {
                    // package was removed
                    comparison.RemovedPackages.Add(oldPackage);
                    continue;
                }

                // do class-level comparison
                var changes = new CoverturaReportComparison.PackageChanges
                {
                    Old = oldPackage,
                    New = newPackage,
                    LineCoverageChange = decimal.Round(newPackage.LineRate - oldPackage.LineRate, decimals: 2),
                    BranchCoverageChange = decimal.Round(newPackage.BranchRate - oldPackage.BranchRate, decimals: 2),
                    ComplexityChange = newPackage.Complexity - oldPackage.Complexity,
                };

                foreach (var classKvp in oldPackage.Classes)
                {
                    var oldClass = classKvp.Value;
                    if (!newPackage.Classes.TryGetValue(classKvp.Key, out var newClass))
                    {
                        changes.RemovedClasses.Add(oldClass);
                        continue;
                    }

                    var changeSummary = new CoverturaReportComparison.ClassChanges
                    {
                        Name = oldClass.Name,
                        Filename = oldClass.Filename,
                        LineCoverageChange = decimal.Round(newClass.LineRate - oldClass.LineRate, decimals: 2),
                        BranchCoverageChange = decimal.Round(newClass.BranchRate - oldClass.BranchRate, decimals: 2),
                        ComplexityChange = newClass.Complexity - oldClass.Complexity,
                    };

                    changeSummary.IsSignificantChange = Math.Abs(changeSummary.LineCoverageChange) > SignificantChangeThreshold
                                                     || Math.Abs(changeSummary.BranchCoverageChange) > SignificantChangeThreshold;

                    changes.ClassChanges[classKvp.Key] = changeSummary;

                }

                changes.NewClasses =
                    newPackage.Classes
                             .Where(kvp => !oldPackage.Classes.ContainsKey(kvp.Key))
                             .Select(kvp => kvp.Value)
                             .ToList();

                matchedPackages.Add(kvp.Key, changes);
            }

            comparison.MatchedPackages = matchedPackages.Values.ToList();

            comparison.NewPackages =
                newReport.Packages
                         .Where(kvp => !matchedPackages.ContainsKey(kvp.Key))
                         .Select(kvp => kvp.Value)
                         .ToList();

            return comparison;
        }

        public static string RenderAsMarkdown(
            CoverturaReportComparison comparison,
            int prNumber,
            string oldDownloadLink,
            string newDownloadLink,
            string oldReportLink,
            string newReportLink,
            string oldCommit,
            string newCommit)
        {
            var oldBranchMarkdown = $"[master](https://github.com/DataDog/dd-trace-dotnet/tree/{oldCommit})";
            var newBranchMarkdown = $"#{prNumber}";
            var prFiles = $"https://github.com/DataDog/dd-trace-dotnet/pull/{prNumber}/files";
            var tree = $"https://github.com/DataDog/dd-trace-dotnet/tree/{newCommit}";
            var oldReport = comparison.Old;
            var newReport = comparison.New;

            var sb = new StringBuilder($@"## Code Coverage Report :bar_chart:

{GetIcon(comparison.LineCoverageChange)} Merging {newBranchMarkdown} into {oldBranchMarkdown} will {GetDescription(comparison.LineCoverageChange, "line coverage")}
{GetIcon(comparison.BranchCoverageChange)} Merging {newBranchMarkdown} into {oldBranchMarkdown} will {GetDescription(comparison.BranchCoverageChange, "branch coverage")}
{GetIcon(-comparison.ComplexityChange)} Merging {newBranchMarkdown} into {oldBranchMarkdown} will {GetComplexityDescription(comparison.ComplexityChange)}


|           | {oldBranchMarkdown} | {newBranchMarkdown}       | Change   | 
|:----------|:-----------:|:-----------:|:--------:|
| Lines     | `{oldReport.LinesCovered}` / `{oldReport.LinesValid}` | `{newReport.LinesCovered}` / `{newReport.LinesValid}` |          |
| Lines %   | `{oldReport.LineRate:P0}`      | `{newReport.LineRate:P0}`      |  `{comparison.LineCoverageChange:P0}` {GetIcon(comparison.LineCoverageChange)}  |
| Branches  | `{oldReport.BranchesCovered}` / `{oldReport.BranchesValid}` | `{newReport.BranchesCovered}` / `{newReport.BranchesValid}` |          |
| Branches %| `{oldReport.BranchRate:P0}`      | `{newReport.BranchRate:P0}`      |  `{comparison.BranchCoverageChange:P0}` {GetIcon(comparison.BranchCoverageChange)}  |
| Complexity|   `{oldReport.Complexity}`      | `{newReport.Complexity}`        |  `{comparison.ComplexityChange}`  {GetIcon(-comparison.ComplexityChange)}    |

View the full report for further details:

* [HTML report for master]({oldReportLink}) | [Source file]({oldDownloadLink})  
* [HTML report for this PR #{prNumber}]({newReportLink}) | [Source file]({newDownloadLink})

");
              foreach (var package in comparison.MatchedPackages)
              {
                  sb.Append($@"### {package.New.Name} Breakdown {GetIcon(package.LineCoverageChange)}

|        | {oldBranchMarkdown} | {newBranchMarkdown}       | Change   | 
|:-------|:-----------:|:-----------:|:--------:|
| Lines %| `{package.Old.LineRate:P0}`      | `{package.New.LineRate:P0}`       |  `{package.LineCoverageChange:P0}` {GetIcon(package.LineCoverageChange)}  |
| Branches %| `{package.Old.BranchRate:P0}`      | `{package.New.BranchRate:P0}`       |  `{package.BranchCoverageChange:P0}` {GetIcon(package.BranchCoverageChange)}  |
| Complexity| `{package.Old.Complexity}`      | `{package.New.Complexity}`       |  `{package.ComplexityChange}` {GetIcon(-package.ComplexityChange)}  |
");

                  var changes = package.ClassChanges.Values
                                       .Where(change => !IgnoredClassPrefixes.Any(toIgnore => change.Name.StartsWith(toIgnore)))
                                       .ToList();
                  if (changes.Any())
                  {
                      sb.Append($@"
The following classes have significant coverage changes.

| File    | Line coverage change | Branch coverage change | Complexity change |
|:--------|:--------------------:|:----------:|:--------:|");

                      var maxFileDisplay = 10;
                      var significantChanges = changes
                                              .Where(x => x.IsSignificantChange)
                                              .OrderBy(x => x.LineCoverageChange)
                                              .ThenBy(x => x.BranchCoverageChange)
                                              .ThenBy(x => x.Name)
                                              .ToList();
                      foreach (var classChange in significantChanges.Take(maxFileDisplay))
                      {
                          var change = classChange;
                          sb.Append($@"
| [{change.Name}]({FixFilename(change.Filename)}) | `{change.LineCoverageChange:P0}` {GetIcon(change.LineCoverageChange)} | `{change.BranchCoverageChange:P0}` {GetIcon(change.BranchCoverageChange)}   | `{change.ComplexityChange}` {GetIcon(-change.ComplexityChange)} |");
                      }

                      var extras = significantChanges.Count - maxFileDisplay;
                      if (extras > 0)
                      {
                          sb.Append($@"
| ...And {extras} more  | | | |");
                      }

                      sb.AppendLine().AppendLine();
                  }

                  if (package.NewClasses.Any())
                  {
                      sb.Append($@"
The following classes were added in {newBranchMarkdown}:

| File    | Line coverage | Branch coverage | Complexity |
|:--------|:--------------------:|:----------:|:--------:|");

                      var maxFileDisplay = 5;
                      foreach (var newClass in package.NewClasses.Take(maxFileDisplay))
                      {
                          sb.Append($@"
| [{FixClassName(newClass.Name)}]({prFiles}) | `{newClass.LineRate:P0}` | `{newClass.BranchRate:P0}` | `{newClass.Complexity}` |");
                      }

                      var extras = package.NewClasses.Count - maxFileDisplay;
                      if (extras > 0)
                      {
                          sb.Append($@"
| ...And {extras} more  | | | |");
                      }

                      sb.AppendLine().AppendLine();
                  }

                  if (package.RemovedClasses.Any())
                  {
                      sb.AppendLine($@"
{package.RemovedClasses.Count} classes were removed from {package.New.Name} in {newBranchMarkdown}")
                        .AppendLine();
                  }
              }

              if(comparison.NewPackages.Any())
              {
                  sb.Append($@"### New projects

{comparison.NewPackages.Count} were added in {newBranchMarkdown}:


| Project    | Line coverage | Branch coverage | Complexity |
|:--------|:--------------------:|:----------:|:--------:|");

                  var maxDisplay = 5;
                  foreach (var newProject in comparison.NewPackages.Take(maxDisplay))
                  {
                      sb.Append($@"
| [{newProject.Name}]({prFiles}) | `{newProject.LineRate:P0}` | `{newProject.BranchRate:P0}` | `{newProject.Complexity}` |");
                  }

                  var extras = comparison.NewPackages.Count - maxDisplay;
                  if (extras > 0)
                  {
                      sb.Append($@"
| ...And {extras} more  | | | |");
                  }
              }

              if (comparison.RemovedPackages.Any())
              {
                  sb.AppendLine($@"### Deleted projects


{comparison.RemovedPackages.Count} projects were removed in {newBranchMarkdown}

| Project    | Line coverage | Branch coverage | Complexity |
|:--------|:--------------------:|:----------:|:--------:|");

                  var maxDisplay = 5;
                  foreach (var oldProject in comparison.RemovedPackages.Take(maxDisplay))
                  {
                      sb.Append($@"
| [{oldProject.Name}]({prFiles}) | `{oldProject.LineRate:P0}` | `{oldProject.BranchRate:P0}` | `{oldProject.Complexity}` |");
                  }

                  var extras = comparison.RemovedPackages.Count - maxDisplay;
                  if (extras > 0)
                  {
                      sb.Append($@"
| ...And {extras} more  | | | |");
                  }
              }

              sb.AppendLine($@"
View the full reports for further details:

* [HTML report for master]({oldReportLink}) | [Source file]({oldDownloadLink})  
* [HTML report for this PR #{prNumber}]({newReportLink}) | [Source file]({newDownloadLink})");

              static string GetIcon(decimal value) => value switch
              {
                  <= -SignificantChangeThreshold => ":no_entry:",
                  >= 0 => ":heavy_check_mark:",
                  _ => ":warning:",
              };

              static string GetDescription(decimal value, string metric) => value switch
              {
                  < 0 => $"will **decrease** {metric} by `{-value:P0}`",
                  > 0 => $"will **increase** {metric} by `{value:P0}`",
                  _ => $"**not change** {metric}",
              };

              static string GetComplexityDescription(int value) => value switch
              {
                  < 0 => $"will **decrease** complexity by `{-value}`",
                  > 0 => $"will **increase** complexity by `{value}`",
                  _ => $"**not change** complexity",
              };

              static string FixClassName(string className) => className.Replace("`", "\\`");

              string FixFilename(string filename)
              {
                  return tree + filename
                               .Substring(8) // remove azdo file path prefix
                               .Replace('\\', '/');
              }

              return sb.ToString();
        }
    }
}
