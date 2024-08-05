// <copyright file="SystemXmlAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> Xpath injection class aspect </summary>
[AspectClass("System.Xml,System.Xml.ReaderWriter,System.Xml.XPath.XDocument", AspectType.Sink, VulnerabilityType.XPathInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class SystemXmlAspect
{
    /// <summary>
    /// Launches a xpath injection vulnerability if the input is tainted
    /// </summary>
    /// <param name="xpath">the path in the xml</param>
    /// <returns>the path parameter</returns>
    [AspectMethodInsertBefore("System.Xml.XmlNode::SelectNodes(System.String)")]
    [AspectMethodInsertBefore("System.Xml.XmlNode::SelectNodes(System.String,System.Xml.XmlNamespaceManager)", 1)]
    [AspectMethodInsertBefore("System.Xml.XmlNode::SelectSingleNode(System.String)")]
    [AspectMethodInsertBefore("System.Xml.XmlNode::SelectSingleNode(System.String,System.Xml.XmlNamespaceManager)", 1)]
    [AspectMethodInsertBefore("System.Xml.XPath.XPathExpression::Compile(System.String)")]
    [AspectMethodInsertBefore("System.Xml.XPath.XPathExpression::Compile(System.String,System.Xml.IXmlNamespaceResolver)", 1)]
    [AspectMethodInsertBefore("System.Xml.XPath.XPathNavigator::Evaluate(System.String)")]
    [AspectMethodInsertBefore("System.Xml.XPath.XPathNavigator::Evaluate(System.String,System.Xml.IXmlNamespaceResolver)", 1)]
    [AspectMethodInsertBefore("System.Xml.XPath.XPathNavigator::SelectSingleNode(System.String)")]
    [AspectMethodInsertBefore("System.Xml.XPath.XPathNavigator::SelectSingleNode(System.String,System.Xml.IXmlNamespaceResolver)", 1)]
    [AspectMethodInsertBefore("System.Xml.XPath.XPathNavigator::Select(System.String)")]
    [AspectMethodInsertBefore("System.Xml.XPath.XPathNavigator::Select(System.String,System.Xml.IXmlNamespaceResolver)", 1)]
    public static string ReviewPath(string xpath)
    {
        try
        {
            IastModule.OnXpathInjection(xpath);
            return xpath;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(SystemXmlAspect)}.{nameof(ReviewPath)}");
            return xpath;
        }
    }
}
