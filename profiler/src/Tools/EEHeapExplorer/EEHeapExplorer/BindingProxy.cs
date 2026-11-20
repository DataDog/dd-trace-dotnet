// <copyright file="BindingProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Windows;

namespace EEHeapExplorer;

/// <summary>
/// A <see cref="Freezable"/> that carries a data object into namescopes that are otherwise unreachable
/// by <c>ElementName</c>/<c>RelativeSource</c> bindings (for example the <c>GroupStyle</c> container
/// template). Freezables participate in the inheritance context, so a proxy placed in an element's
/// resources can expose arbitrary data to control templates.
/// </summary>
public sealed class BindingProxy : Freezable
{
    /// <summary>Identifies the <see cref="Data"/> dependency property.</summary>
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));

    /// <summary>Gets or sets the wrapped data object exposed to bindings.</summary>
    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <inheritdoc/>
    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
