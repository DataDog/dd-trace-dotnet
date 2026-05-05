// <copyright file="CategoryCodeToNameConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Globalization;
using System.Windows.Data;
using ReferenceChainModel;

namespace ReferenceChainExplorer.Converters;

/// <summary>
/// Converts a root category code (e.g., "K") to a human-readable name (e.g., "Stack").
/// </summary>
public class CategoryCodeToNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string code)
        {
            return code.Contains(',')
                ? RootCategoryHelper.GetCategoryNamesForDisplay(code)
                : RootCategoryHelper.GetCategoryName(code);
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
