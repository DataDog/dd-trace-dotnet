// <copyright file="GroupHeaderConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Globalization;
using System.Linq;
using System.Windows.Data;
using EEHeapModel;

namespace EEHeapExplorer.Converters;

/// <summary>
/// Builds the label for a grouped section of the kind grid: the group name plus the number of kinds
/// it contains (the numeric totals are rendered separately, aligned to the grid columns).
/// </summary>
public class GroupHeaderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not CollectionViewGroup group)
        {
            return string.Empty;
        }

        int count = group.Items.Count(static i => i is KindSummary);
        string kinds = count == 1 ? "1 kind" : $"{count} kinds";
        return $"{group.Name}  ({kinds})";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
