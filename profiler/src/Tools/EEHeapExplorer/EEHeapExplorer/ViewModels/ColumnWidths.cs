// <copyright file="ColumnWidths.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EEHeapExplorer.ViewModels;

/// <summary>
/// Live, observable mirror of the top grid's column widths. The code-behind keeps these in sync with
/// the real <see cref="System.Windows.Controls.DataGridColumn.ActualWidth"/> values (which are not
/// dependency properties and therefore cannot be bound directly), so the grouped header cells can bind
/// to them and stay aligned with the data columns as they are resized.
/// </summary>
public sealed class ColumnWidths : INotifyPropertyChanged
{
    private double _kind;
    private double _count;
    private double _reserved;
    private double _committed;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the width of the Kind column.</summary>
    public double Kind
    {
        get => _kind;
        set => Set(ref _kind, value);
    }

    /// <summary>Gets or sets the width of the Count column.</summary>
    public double Count
    {
        get => _count;
        set => Set(ref _count, value);
    }

    /// <summary>Gets or sets the width of the Reserved column.</summary>
    public double Reserved
    {
        get => _reserved;
        set => Set(ref _reserved, value);
    }

    /// <summary>Gets or sets the width of the Committed column.</summary>
    public double Committed
    {
        get => _committed;
        set => Set(ref _committed, value);
    }

    private void Set(ref double field, double value, [CallerMemberName] string? propertyName = null)
    {
        // Avoid raising for sub-pixel churn from repeated layout passes.
        if (Math.Abs(field - value) < 0.5)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
