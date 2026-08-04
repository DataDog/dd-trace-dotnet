// <copyright file="GroupSumConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Globalization;
using System.Windows.Data;
using EEHeapModel;

namespace EEHeapExplorer.Converters;

/// <summary>
/// Aggregates the <see cref="KindSummary"/> items of a grouped section so the group header can render
/// totals aligned to the grid columns. The <c>ConverterParameter</c> selects what to return:
/// <list type="bullet">
/// <item><c>Count</c> - total region count (formatted).</item>
/// <item><c>ReservedBytes</c> / <c>CommittedBytes</c> - total size (human-readable string).</item>
/// <item><c>ReservedFraction</c> / <c>CommittedFraction</c> - group's share of the report (0..1, for the bar).</item>
/// </list>
/// </summary>
public class GroupSumConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string mode = parameter as string ?? string.Empty;

        if (value is not CollectionViewGroup group)
        {
            return mode is "ReservedFraction" or "CommittedFraction" ? 0d : string.Empty;
        }

        ulong reserved = 0;
        ulong committed = 0;
        long count = 0;
        double reservedFraction = 0;
        double committedFraction = 0;

        foreach (var item in group.Items)
        {
            if (item is KindSummary k)
            {
                reserved += k.ReservedTotal;
                committed += k.CommittedTotal;
                count += k.Count;
                reservedFraction += k.ReservedFraction;
                committedFraction += k.CommittedFraction;
            }
        }

        return mode switch
        {
            "Count" => count.ToString("N0", culture),
            "ReservedBytes" => ByteSizeConverter.Format(reserved),
            "CommittedBytes" => ByteSizeConverter.Format(committed),
            "ReservedFraction" => Math.Min(reservedFraction, 1.0),
            "CommittedFraction" => Math.Min(committedFraction, 1.0),
            _ => string.Empty,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
