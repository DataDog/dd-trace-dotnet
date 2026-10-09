// <copyright file="ByteSizeConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Globalization;
using System.Windows.Data;

namespace EEHeapExplorer.Converters;

/// <summary>
/// Formats a byte count as a human-readable size (B, KB, MB, GB).
/// </summary>
public class ByteSizeConverter : IValueConverter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    /// <summary>
    /// Formats a byte count as a human-readable size (B, KB, MB, GB, TB).
    /// </summary>
    public static string Format(double bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        int unit = 0;
        while (bytes >= 1024 && unit < Units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes:N0} {Units[unit]}"
            : $"{bytes:N2} {Units[unit]}";
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double bytes = value switch
        {
            ulong u => u,
            long l => l,
            int i => i,
            uint ui => ui,
            double d => d,
            _ => 0,
        };

        return Format(bytes);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
