using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// ReSharper disable InconsistentNaming

namespace NativeValidation;

/// <summary>
/// Lists the symbols that are provided by various dependencies
/// </summary>
public static class Symbols
{
    static Symbols()
    {
        // read the symbols as embedded resources
        var assembly = typeof(Symbols).Assembly;
        using var muslStream = assembly.GetManifestResourceStream("NativeValidation.alpine3_14_Musl1_2_2.txt");
        using var libgccStream = assembly.GetManifestResourceStream("NativeValidation.alpine3_14_Libgcc10_3_1.txt");

        var hashset = new HashSet<string>(1850); // we know how many there should be
        AddSymbols(hashset, muslStream);
        AddSymbols(hashset, libgccStream);
        Alpine3_14 = hashset;

        static void AddSymbols(HashSet<string> set, Stream symbols)
        {
            using var reader = new StreamReader(symbols, Encoding.UTF8, leaveOpen: true);

            string line;
            while ((line = reader.ReadLine()) is not null)
            {
                var span = line.AsSpan().Trim();
                if (span.Length == 0)
                {
                    continue;
                }

                var index = span.LastIndexOf(' ');
                if (index >= 0 && index + 1 < span.Length)
                {
                    var symbol = span.Slice(index + 1).ToString();
                    set.Add(symbol);
                }
            }
        }
    }

    /// <summary>
    /// The symbols provided by alpine:3.14 (musl 1.2.2 + libgcc) which is what we currently support
    /// These  are the only undefined symbols allowed to be used by our musl-based (or universal) binaries
    /// The musl library will always be available, and the libgcc library is a dependency of .NET from
    /// _at least_ .NET Core 3.1, so we assume it's available.
    /// <see href="https://github.com/dotnet/core/blob/1d14a83f56ae8f58ef8446b623a4d47b6cfc4baf/release-notes/3.1/linux-packages.md#packages" />
    /// </summary>
    /// <returns>The list of symbols exposed by Alpine</returns>
    /// <remarks>
    /// The list of musl symbols was found by running an alpine:3.14 image and running
    /// nm -D --defined-only /lib/ld-musl-x86_64.so.1
    ///
    /// You can see the full list of symbols in
    /// - alpine3_14_Musl1_2_2.txt
    /// - alpine3_14_Libgcc10_3_1.txt
    /// </remarks>
    public static HashSet<string> Alpine3_14 { get; }
}
