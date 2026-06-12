using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiffMatchPatch;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using static Nuke.Common.Utilities.StringExtensions;
using Logger = Serilog.Log;

namespace NativeValidation;

public class NativeValidationHelper
{
    public NativeValidationHelper(Lazy<Tool> nm, bool isAlpine, AbsolutePath buildProjectDirectory)
    {
        Nm = nm;
        IsAlpine = isAlpine;
        BuildProjectDirectory = buildProjectDirectory;
    }

    AbsolutePath BuildProjectDirectory { get; }
    Lazy<Tool> Nm { get; }
    bool IsAlpine { get; }

    public void ValidateNativeLibraryCompatibility(
        AbsolutePath libraryPath,
        Version expectedGlibcVersion,
        string snapshotPrefix,
        IEnumerable<string> allowedSymbols = null)
    {
        ValidateNativeLibraryGlibcCompatibility(libraryPath, expectedGlibcVersion, allowedSymbols);

        if (IsAlpine)
        {
            ValidateNativeSymbols(libraryPath, snapshotPrefix);
        }

    }

    void ValidateNativeLibraryGlibcCompatibility(
        AbsolutePath libraryPath,
        Version expectedGlibcVersion,
        IEnumerable<string> allowedSymbols = null)
    {
        var filename = Path.GetFileNameWithoutExtension(libraryPath);
        var glibcVersion = FindMaxGlibcVersion(libraryPath, allowedSymbols);

        Logger.Information("Maximum required glibc version for {Filename} is {GlibcVersion}", filename, glibcVersion);

        if (IsAlpine && glibcVersion is not null)
        {
            throw new Exception($"Alpine build of {filename} should not have glibc symbols in the binary, but found {glibcVersion}");
        }
        else if (!IsAlpine && glibcVersion != expectedGlibcVersion)
        {
            throw new Exception($"{filename} should have a maximum required glibc version of {expectedGlibcVersion} but has {glibcVersion}");
        }
        
        Version FindMaxGlibcVersion(AbsolutePath libraryPath, IEnumerable<string> allowedSymbols)
        {
            var output = Nm.Value($"--with-symbol-versions -D {libraryPath} ").Select(x => x.Text).ToList();

            // Gives output similar to this:
            // 0000000000170498 T SetGitMetadataForApplication
            // 000000000016f944 T ThreadsCpuManager_Map
            //                  w __cxa_finalize@GLIBC_2.17
            //                  U __cxa_thread_atexit_impl@GLIBC_2.18
            //                  U __duplocale@GLIBC_2.17
            //                  U __environ@GLIBC_2.17
            //                  U __errno_location@GLIBC_2.17
            //                  U __freelocale@GLIBC_2.17
            //                  U __fxstat@GLIBC_2.17
            //                  U __fxstat64@GLIBC_2.17
            //                  U __getdelim@GLIBC_2.17
            //                  w __gmon_start__
            //                  U __iswctype_l@GLIBC_2.17
            //                  U __lxstat@GLIBC_2.17
            //                  U __newlocale@GLIBC_2.17
            //
            // We only care about the Undefined symbols that are in glibc
            // In this example, we will return 2.18

            return output
                  .Where(x => x.Contains("@GLIBC_") && allowedSymbols?.Any(y => x.Contains(y)) != true)
                  .Select(x => Version.Parse(x.Substring(x.IndexOf("@GLIBC_") + 7)))
                  .Max();
        }
    }

    public void ValidateNativeSymbols(AbsolutePath libraryPath, string snapshotNamePrefix)
    {
        var output = Nm.Value($"-D {libraryPath}").Select(x => x.Text).ToList();

        // Gives output similar to this:
        // 0000000000006bc8 D DdDotnetFolder
        // 0000000000006bd0 D DdDotnetMuslFolder
        //                  w _ITM_deregisterTMCloneTable
        //                  w _ITM_registerTMCloneTable
        //                  w __cxa_finalize
        //                  w __deregister_frame_info
        //                  U __errno_location
        //                  U __tls_get_addr
        // 0000000000002d1b T _fini
        // 0000000000002d18 T _init
        // 0000000000003d70 T accept
        // 0000000000003e30 T accept4
        //                  U access
        //
        // The types of symbols are:
        // D: Data section symbol. These symbols are initialized global variables.
        // w: Weak symbol. These symbols are weakly referenced and can be overridden by other symbols.
        // U: Undefined symbol. These symbols are referenced in the file but defined elsewhere.
        // T: Text section symbol. These symbols are functions or executable code.
        // B: BSS (Block Started by Symbol) section symbol. These symbols are uninitialized global variables.
        //
        // We only care about the Undefined symbols - we don't want to accidentally add more of them

        Logger.Debug("NM output: {Output}", string.Join(Environment.NewLine, output));

        var symbols = output
                     .Select(x => x.Trim())
                     .Where(x => x.StartsWith("U "))
                     .Select(x => x.TrimStart("U "))
                     .OrderBy(x => x)
                     .ToList();

        var libraryName = Path.GetFileNameWithoutExtension(libraryPath);

        var onlyHasValidAlpineSymbols = OnlyHasValidAlpineSymbols();
        var hasValidSnapshot = HasValidSnapshot();

        if (onlyHasValidAlpineSymbols && hasValidSnapshot)
        {
            return;
        }

        throw new Exception("There were problems with the symbols exposed by the native libraries. Please see previous messages for details");

        bool OnlyHasValidAlpineSymbols()
        {
            var allowedSymbols = Symbols.Alpine3_14;

            // We assume all the ddog_ and blaze_ symbols are provided by libdatadog and leave it at that
            var missingSymbols = symbols
                                .Where(x => !(
                                                 x.StartsWith("ddog_", StringComparison.OrdinalIgnoreCase)
                                              || x.StartsWith("blaze_", StringComparison.OrdinalIgnoreCase)
                                              || allowedSymbols.Contains(x)))
                                .ToList();

            if (missingSymbols.Count == 0)
            {
                Logger.Information("All Undefined symbols in {LibraryName} are available in alpine:3.14", libraryName);
                return true;
            }

            var symbolList = string.Join(Environment.NewLine, missingSymbols);
            Logger.Error("Found Undefined symbols in {LibraryName} which are not provided by musl or libgcc{Break}:{Symbols}", libraryName, Environment.NewLine, symbolList);
            return false;
        }

        bool HasValidSnapshot()
        {
            var received = string.Join(Environment.NewLine, symbols);
            var verifiedPath = BuildProjectDirectory / nameof(NativeValidation) / $"{snapshotNamePrefix}.verified.txt";
            var verified = File.Exists(verifiedPath)
                               ? File.ReadAllText(verifiedPath)
                               : string.Empty;

            Logger.Information("Comparing snapshot of Undefined symbols in the {LibraryName} using {Path}...", libraryName, verifiedPath);

            var dmp = new diff_match_patch();
            var diff = dmp.diff_main(verified, received);
            dmp.diff_cleanupSemantic(diff);

            var changedSymbols = diff
                                .Where(x => x.operation != Operation.EQUAL)
                                .Select(x => x.text.Trim())
                                .ToList();

            if (changedSymbols.Count == 0)
            {
                Logger.Information("No changes found in Undefined symbols in {LibraryName}", libraryName);
                return true;
            }

            // Print the expected values, so it's easier to copy-paste them into the snapshot file as required
            Logger.Information("Received snapshot for {LibraryName}{Break}{Symbols}", libraryName, Environment.NewLine, received);
            Logger.Information("Expected snapshot for {LibraryName}{Break}{Symbols}", libraryName, Environment.NewLine, verified);

            Logger.Information("Changed symbols for {LibraryName}:", libraryName);

            DiffHelper.PrintDiff(diff);

            Logger.Error("Found differences in undefined symbols in {LibraryName}. These are shown above as both a diff and the" +
                                "full expected snapshot. Verify that these changes are expected, and will not cause problems. " +
                                "Removing symbols is generally a safe operation, but adding them could cause crashes. " +
                                "If the new symbols are safe to add, update the snapshot file at {VerifiedPath} with the " +
                                "new values", libraryName, verifiedPath);

            return true;
        }
    }
}
