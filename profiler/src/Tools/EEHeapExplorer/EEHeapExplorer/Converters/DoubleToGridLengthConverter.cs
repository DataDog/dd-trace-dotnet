// <copyright file="DoubleToGridLengthConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EEHeapExplorer.Converters;

/// <summary>
/// Converts a pixel width (double) into an absolute <see cref="GridLength"/> so a group-header
/// <see cref="System.Windows.Controls.ColumnDefinition"/> can track a live data-grid column width.
/// </summary>
public class DoubleToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var width = value is double d && d > 0 ? d : 0;
        return new GridLength(width, GridUnitType.Pixel);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is GridLength length ? length.Value : 0d;
    }
}
