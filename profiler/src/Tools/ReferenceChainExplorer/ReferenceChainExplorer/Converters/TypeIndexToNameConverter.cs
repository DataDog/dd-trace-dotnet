// <copyright file="TypeIndexToNameConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Globalization;
using System.Windows.Data;
using ReferenceChainModel;

namespace ReferenceChainExplorer.Converters;

/// <summary>
/// Converts a type index (int) to a type name string by looking up the TypeTable.
/// The TypeTable is set once when a file is loaded — no strings are stored in model or ViewModel objects.
/// Use ConverterParameter="short" to get the short name (after last '.').
/// </summary>
public class TypeIndexToNameConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets the type table (string interning table from the JSON "tt" array).
    /// Set this property when a file is loaded.
    /// </summary>
    public IReadOnlyList<string>? TypeTable { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && TypeTable is not null && index >= 0 && index < TypeTable.Count)
        {
            string fullName = TypeTable[index];

            if ("short".Equals(parameter as string, StringComparison.Ordinal))
            {
                return ReferenceTree.GetShortTypeNameFromFullName(fullName);
            }

            return fullName;
        }

        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
