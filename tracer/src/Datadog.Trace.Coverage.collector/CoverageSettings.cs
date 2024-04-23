// <copyright file="CoverageSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Xml;
using Datadog.Trace.Ci.Configuration;

namespace Datadog.Trace.Coverage.Collector;

/// <summary>
/// Coverage settings
/// </summary>
internal class CoverageSettings
{
    public CoverageSettings(XmlElement? configurationElement, string? tracerHome, CIVisibilitySettings? ciVisibilitySettings = null)
    {
        TracerHome = tracerHome;
        CIVisibility = ciVisibilitySettings ?? CIVisibilitySettings.FromDefaultSources();

        if (configurationElement is not null)
        {
            IReadOnlyList<string> excludeFilters = new List<string>(),
                                  excludeSourceFiles = new List<string>(),
                                  excludeByAttribute = new List<string>();
            GetStringArrayFromXmlElement(configurationElement["Exclude"], ref excludeFilters, FiltersHelper.IsValidFilterExpression);
            GetStringArrayFromXmlElement(configurationElement["ExcludeByFile"], ref excludeSourceFiles);
            GetStringArrayFromXmlElement(configurationElement["ExcludeByAttribute"], ref excludeByAttribute);
            GetStringArrayFromXmlNodeList(configurationElement["CodeCoverage"]?["Sources"]?["Exclude"]?.ChildNodes, ref excludeSourceFiles);
            GetStringArrayFromXmlNodeList(configurationElement["CodeCoverage"]?["Attributes"]?["Exclude"]?.ChildNodes, ref excludeByAttribute);
            ExcludeFilters = excludeFilters;
            ExcludeSourceFiles = excludeSourceFiles;
            ExcludeByAttribute = excludeByAttribute;
        }
        else
        {
            ExcludeFilters = Array.Empty<string>();
            ExcludeSourceFiles = Array.Empty<string>();
            ExcludeByAttribute = Array.Empty<string>();
        }
    }

    public CoverageSettings(XmlElement? configurationElement)
        : this(configurationElement, Util.EnvironmentHelpers.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME"), CIVisibilitySettings.FromDefaultSources())
    {
    }

    /// <summary>
    /// Gets the filters to exclude
    /// </summary>
    public IReadOnlyList<string> ExcludeFilters { get; }

    /// <summary>
    /// Gets the source files to exclude
    /// </summary>
    public IReadOnlyList<string> ExcludeSourceFiles { get; }

    /// <summary>
    /// Gets the attributes to exclude
    /// </summary>
    public IReadOnlyList<string> ExcludeByAttribute { get; }

    /// <summary>
    /// Gets the tracer home path
    /// </summary>
    public string? TracerHome { get; }

    /// <summary>
    /// Gets the CI Visibility settings
    /// </summary>
    public CIVisibilitySettings CIVisibility { get; }

    private static void GetStringArrayFromXmlElement(XmlElement? xmlElement, ref IReadOnlyList<string> elements, Func<string?, bool>? validator = null)
    {
        if (xmlElement?.InnerText is { } elementText &&
            elementText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } elementsArray)
        {
            var lstElements = (List<string>)elements;
            foreach (var item in elementsArray)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                var value = item.Trim();
                if (!lstElements.Contains(value))
                {
                    if (validator is null || validator(value))
                    {
                        lstElements.Add(value);
                    }
                }
            }
        }
    }

    private static void GetStringArrayFromXmlNodeList(XmlNodeList? xmlNodeList, ref IReadOnlyList<string> elements)
    {
        if (xmlNodeList is { } nodeList)
        {
            var lstElements = (List<string>)elements;
            foreach (XmlElement element in nodeList)
            {
                var item = element.InnerText;
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                var value = item.Trim();
                if (!lstElements.Contains(value))
                {
                    lstElements.Add(value);
                }
            }
        }
    }
}
