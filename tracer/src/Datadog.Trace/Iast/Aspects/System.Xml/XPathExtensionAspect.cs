// <copyright file="XPathExtensionAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> Xpath inection class aspect </summary>
[AspectClass("System.Xml.XPath.XDocument,System.Xml.Linq", AspectType.Sink, VulnerabilityType.XPathInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]

public class XPathExtensionAspect
{
    /// <summary>
    /// Launches a spath injection vulnerability if the input is tainted
    /// </summary>
    /// <param name="xpath">the path in the xml</param>
    /// <returns>the path parameter</returns>
    [AspectMethodInsertBefore("System.Xml.XPath.Extensions::XPathEvaluate(System.Xml.Linq.XNode,System.String)")]
    [AspectMethodInsertBefore("System.Xml.XPath.Extensions::XPathEvaluate(System.Xml.Linq.XNode,System.String,System.Xml.IXmlNamespaceResolver)", 1)]
    [AspectMethodInsertBefore("System.Xml.XPath.Extensions::XPathSelectElement(System.Xml.Linq.XNode,System.String)")]
    [AspectMethodInsertBefore("System.Xml.XPath.Extensions::XPathSelectElement(System.Xml.Linq.XNode,System.String,System.Xml.IXmlNamespaceResolver)", 1)]
    [AspectMethodInsertBefore("System.Xml.XPath.Extensions::XPathSelectElements(System.Xml.Linq.XNode,System.String)")]
    [AspectMethodInsertBefore("System.Xml.XPath.Extensions::XPathSelectElements(System.Xml.Linq.XNode,System.String,System.Xml.IXmlNamespaceResolver)", 1)]
    public static string ReviewPath(string xpath)
    {
        try
        {
            IastModule.OnXpathInjection(xpath);
            return xpath;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(XPathExtensionAspect)}.{nameof(ReviewPath)}");
            return xpath;
        }
    }
}
