﻿//------------------------------------------------------------------------------
// <auto-generated />
// This file was imported directly from https://github.com/Suchiman/SerilogAnalyzer/blob/bf62860f502db19bc45fd0f46541f383ef3a4455/SerilogAnalyzer/SerilogAnalyzer/PropertyToken.cs
// and updated for use in this project
//------------------------------------------------------------------------------

// Copyright 2013-2015 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#nullable enable
using System;
using System.Globalization;

namespace Datadog.Trace.Tools.Analyzers.LogAnalyzer.Helpers;

/// <summary>
/// A message template token representing a log event property.
/// </summary>
sealed class PropertyToken : MessageTemplateToken
{
    readonly string _rawText;
    readonly int? _position;

    public PropertyToken(int startIndex, string propertyName, string rawText)
        : base(startIndex, rawText.Length)
    {
        PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _rawText = rawText ?? throw new ArgumentNullException(nameof(rawText));

        int position;
        if (int.TryParse(PropertyName, NumberStyles.None, CultureInfo.InvariantCulture, out position) &&
            position >= 0)
        {
            _position = position;
        }
    }

    /// <summary>
    /// The property name.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// True if the property name is a positional index; otherwise, false.
    /// </summary>
    public bool IsPositional => _position.HasValue;

    internal string RawText => _rawText;

    /// <summary>
    /// Try to get the integer value represented by the property name.
    /// </summary>
    /// <param name="position">The integer value, if present.</param>
    /// <returns>True if the property is positional, otherwise false.</returns>
    public bool TryGetPositionalValue(out int position)
    {
        if (_position == null)
        {
            position = 0;
            return false;
        }

        position = _position.Value;
        return true;
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>
    /// A string that represents the current object.
    /// </returns>
    /// <filterpriority>2</filterpriority>
    public override string ToString() => _rawText;
}
