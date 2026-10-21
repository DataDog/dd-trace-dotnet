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

/// <summary>
/// Helper class for validating native libraries. We keep the check on __libc_single_threaded to make sure
/// that we don't accidentally use a newer version of the glibc (appeared in 2.32).
/// </summary>
public class NativeValidationHelper
{
    static readonly string[] ForbiddenDynamicSymbols = { "__libc_single_threaded" };

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
        Version maximumAllowedGlibcVersion,
        string snapshotPrefix,
        IEnumerable<string> allowedSymbols = null)
    {
        var symbols = ReadDynamicSymbols(libraryPath, includeSymbolVersions: true);
        ValidateNoForbiddenDynamicSymbols(symbols, libraryPath);
        ValidateNativeLibraryGlibcCompatibility(symbols, libraryPath, maximumAllowedGlibcVersion, allowedSymbols);

        if (IsAlpine)
        {
            ValidateNativeSymbolsCore(symbols, libraryPath, snapshotPrefix);
        }
    }

    void ValidateNativeLibraryGlibcCompatibility(
        IReadOnlyList<NmSymbol> symbols,
        AbsolutePath libraryPath,
        Version maximumAllowedGlibcVersion,
        IEnumerable<string> allowedSymbols = null)
    {
        var filename = Path.GetFileNameWithoutExtension(libraryPath);
        var scanResult = GlibcSymbolVersionChecker.Scan(symbols, allowedSymbols);
        var maximumRequiredVersion = GlibcSymbolVersionChecker.GetMaximumVersion(scanResult);

        Logger.Information("Maximum required glibc version for {Filename} is {GlibcVersion}", filename, maximumRequiredVersion);

        GlibcSymbolVersionChecker.Validate(
            scanResult,
            maximumAllowedGlibcVersion,
            IsAlpine,
            filename);
    }

    public void ValidateNativeSymbols(AbsolutePath libraryPath, string snapshotNamePrefix)
    {
        var symbols = ReadDynamicSymbols(libraryPath, includeSymbolVersions: false);
        ValidateNoForbiddenDynamicSymbols(symbols, libraryPath);
        ValidateNativeSymbolsCore(symbols, libraryPath, snapshotNamePrefix);
    }

    IReadOnlyList<NmSymbol> ReadDynamicSymbols(AbsolutePath libraryPath, bool includeSymbolVersions)
    {
        var symbolVersionArgument = includeSymbolVersions ? "--with-symbol-versions " : string.Empty;
        var output = Nm.Value($"{symbolVersionArgument}-D \"{libraryPath}\"", logOutput: false)
                       .Where(x => x.Type == OutputType.Std)
                       .Select(x => x.Text)
                       .ToList();

        Logger.Debug("NM output: {Output}", string.Join(Environment.NewLine, output));
        return NmSymbolParser.Parse(output);
    }

    void ValidateNoForbiddenDynamicSymbols(IReadOnlyList<NmSymbol> symbols, AbsolutePath libraryPath)
    {
        var forbiddenSymbols = NativeExportChecker.FindForbiddenSymbols(symbols, ForbiddenDynamicSymbols);
        if (forbiddenSymbols.Count == 0)
        {
            return;
        }

        var filename = Path.GetFileNameWithoutExtension(libraryPath);
        throw new InvalidOperationException(
            $"{filename} imports or exports symbols that are forbidden by the target libc contract:{Environment.NewLine}"
          + string.Join(Environment.NewLine, forbiddenSymbols.Select(x => $"  {x}")));
    }

    void ValidateNativeSymbolsCore(
        IReadOnlyList<NmSymbol> dynamicSymbols,
        AbsolutePath libraryPath,
        string snapshotNamePrefix)
    {
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
        // We only care about the Undefined symbols - we don't want to accidentally add more of them.
        // Newer nm (binutils >= 2.35) prints ELF version tags by default (@GLIBC_2.17, @GCC_3.3);
        // older nm does not. NmSymbolParser exposes both bare and versioned names, so snapshots
        // and the musl allowlist use bare names while the glibc ceiling check uses versions.

        var symbols = dynamicSymbols
                     .Where(x => x.Type == 'U')
                     .Select(x => x.Name)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(x => x, StringComparer.Ordinal)
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
                                                 x.StartsWith("ddog_")
                                              || x.StartsWith("blaze_")
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
                               ? File.ReadAllText(verifiedPath).TrimEnd()
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

            return false;
        }
    }
}
