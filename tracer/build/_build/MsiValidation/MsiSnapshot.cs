using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiffMatchPatch;
using Nuke.Common;
using Nuke.Common.IO;
using WixToolset.Dtf.WindowsInstaller;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Logger = Serilog.Log;

namespace MsiValidation;

static class MsiSnapshot
{
    static readonly string[] Tables =
    {
        "Component", "CustomAction", "Directory", "Environment",
        "Feature", "File", "InstallExecuteSequence", "Property", "Registry"
    };

    // Columns to exclude per table (unstable between builds, not behaviorally significant)
    static readonly Dictionary<string, HashSet<string>> ExcludedColumns = new()
    {
        {"File", new() {"FileSize", "Sequence"}},
    };

    public static void ValidateMsiSnapshot(AbsolutePath msiPath, AbsolutePath verifiedPath, string threePartVersion, string fullVersion)
    {
        Logger.Information("Comparing snapshot of MSI for {MsiPath} using {Path}...", msiPath, verifiedPath);

        var received = Generate(msiPath, threePartVersion, fullVersion);
        var verified = File.Exists(verifiedPath) ? File.ReadAllText(verifiedPath) : string.Empty;

        var dmp = new diff_match_patch();
        var diff = dmp.diff_main(verified, received);
        dmp.diff_cleanupSemantic(diff);

        var changes = diff
            .Where(x => x.operation != Operation.EQUAL)
            .Select(x => x.text.Trim())
            .ToList();

        var msiFilename = Path.GetFileName(msiPath);
        if (changes.Count == 0)
        {
            Logger.Information("No changes found in MSI snapshot for {Msi}", msiFilename);
            return;
        }

        // Print the expected values, so it's easier to copy-paste them into the snapshot file as required
        Logger.Information("Received snapshot for {Msi}{Break}{Symbols}", msiFilename, Environment.NewLine, received);
        Logger.Information("Expected snapshot for {Msi}{Break}{Symbols}", msiFilename, Environment.NewLine, verified);

        Logger.Information("Changed lines for {Msi}:", msiFilename);

        DiffHelper.PrintDiff(diff);

        Logger.Error("Found differences in contents in {Msi}. These are shown above as both a diff and the" +
                     "full expected snapshot. Verify that these changes are expected, before updating the snapshot file" +
                     " at {VerifiedPath} with the new values", msiFilename, verifiedPath);

        throw new Exception("There were problems with the snapshot of the MSI. Please see previous messages for details");
    }

    static string Generate(string msiPath, string threePartVersion, string fullVersion)
    {
        var snapshot = new SortedDictionary<string, object>();
        var sanitizer = new Sanitizer(threePartVersion, fullVersion);

        using var db = new Database(msiPath, DatabaseOpenMode.ReadOnly);
        foreach (var table in Tables)
        {
            if (!db.IsTablePersistent(table))
                continue;

            var columns = db.Tables[table].Columns;
            var primaryKey = columns[0].Name;
            var entries = new SortedDictionary<string, object>();

            using var view = db.OpenView($"SELECT * FROM `{table}`");
            view.Execute();

            foreach (var record in view)
            {
                using (record)
                {
                    var key = record.GetString(primaryKey);
                    if (columns.Count == 2)
                    {
                        // Simple key=value table (e.g., Property)
                        var value = sanitizer.GetSanitizedString(record, columns[1].Name);

                        // ProductCode is auto-generated per build; sanitize to placeholder
                        if (key == "ProductCode")
                        {
                            value = "PRODUCT_CODE";
                        }

                        entries[key] = value;
                    }
                    else
                    {
                        // Multi-column: emit sorted dict of non-key columns
                        var props = new SortedDictionary<string, string>();
                        for (int i = 1; i < columns.Count; i++)
                        {
                            var col = columns[i];
                            if (col.Name == primaryKey)
                            {
                                continue;
                            }

                            if (ExcludedColumns.TryGetValue(table, out var excluded) && excluded.Contains(col.Name))
                            {
                                continue;
                            }

                            var val = sanitizer.GetSanitizedString(record, col.Name);
                            if (val is not null)
                            {
                                props[col.Name] = val;
                            }
                        }

                        entries[key] = props;
                    }
                }
            }

            if (entries.Count > 0)
                snapshot[table] = entries;
        }

        var serializer = new SerializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .DisableAliases()
            .Build();

        return serializer.Serialize(snapshot);
    }

    private class Sanitizer
    {
        readonly string ThreePartVersion;
        readonly string FourPartVersion;
        readonly string FullThreePartVersion;
        readonly string FullFourPartVersion;

        public Sanitizer(string threePartVersion, string fullThreePartVersion)
        {
            ThreePartVersion = threePartVersion;
            FourPartVersion = $"{threePartVersion}.0";
            FullThreePartVersion = fullThreePartVersion;
            FullFourPartVersion = fullThreePartVersion.Replace(ThreePartVersion, FourPartVersion);
        }

        public string GetSanitizedString(Record record, string property)
        {
            var val = record.GetString(property);
            if (val is null)
            {
                return null;
            }

            // sanitize version numbers so they're stable
            if (val == ThreePartVersion)
            {
                return "THREE_PART_VERSION";
            }

            if (val == FourPartVersion)
            {
                return "FOUR_PART_VERSION";
            }

            if (val == FullThreePartVersion)
            {
                return "FULL_VERSION";
            }

            if (val == FullFourPartVersion)
            {
                return "FULL_FOUR_PART_VERSION";
            }

            return val;
        }
    }

}
