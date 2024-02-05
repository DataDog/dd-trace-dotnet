using System.Xml;
using System.Xml.XPath;
using System.IO;
using System.Xml.Linq;
using Xunit;
using FluentAssertions;
using System.Linq;
using System.Collections;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class XMLNodeAspectTests : InstrumentationTestsBase
{
    protected const string notTaintedValue = "nottainted";
    protected static string username = "' or 1=1 or ''='";
    protected static string pass = "nopass";
    protected static string expression;
    protected static string attribute = "/name | ./user";
    private string _findUserXPath;
    private string _evaluateExpression;
    readonly string xmlContent = @"<?xml version=""1.0"" encoding=""ISO-8859-1""?>
                <data>
                <user>
                 <name>jaime</name>
                 <password>1234</password>
                 <account>administrative_account</account>
                </user>
                <user>
                 <name>tom</name>
                 <password>12345</password>
                 <account>toms_acccount</account>
                </user>
                <user>
                 <name>guest</name>
                 <password>anonymous1234</password>
                 <account>guest_account</account>
                </user>
                </data>";

    public XMLNodeAspectTests()
    {
        AddTainted(username);
        AddTainted(pass);
        AddTainted(attribute);
        _findUserXPath = "/data/user[name/text()='" + username +"' and password/text()='" + pass + "}']";
        _evaluateExpression = "/data/user[name/text()='" + username +"' and password/text()='" + pass + "}']/account/text()";
        expression = "./user" + attribute;
    }

    //[AspectMethodInsertBefore("System.Xml.XmlNode::SelectNodes(System.String)")]

    [Fact]
    public void GivenXmlDocument_WhenSelectNode_Vulnerable()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        var result = doc.SelectNodes(_findUserXPath);
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenXmlDocument_WhenSelectNodeNotTainted_NotVulnerable()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        var result = doc.SelectNodes("NotTainted");
        result.Should().NotBeNull();
        result.Count.Should().Be(0);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenXmlDocument_WhenSelectNodeNull_NotVulnerable()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        try
        {
            doc.SelectNodes(null);
            Assert.True(false, "XPathException should be thrown");
        }
        catch (XPathException)
        {
        }

        AssertNotVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XmlNode::SelectNodes(System.String,System.Xml.XmlNamespaceManager)", 1)]

    [Fact]
    public void GivenXmlDocument_WhenSelectNode_Vulnerable2()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        var result = doc.SelectNodes(_findUserXPath, null);
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XmlNode::SelectSingleNode(System.String)")]

    [Fact]
    public void GivenXmlDocument_WhenSelectSingleNode_Vulnerable()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        var result = doc.SelectSingleNode(_findUserXPath);
        result.Should().NotBeNull();
        result.Name.Should().Be("user");
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XmlNode::SelectSingleNode(System.String,System.Xml.XmlNamespaceManager)", 1)]

    [Fact]
    public void GivenXmlDocument_WhenSelectSingleNode_Vulnerable2()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        var result = doc.SelectSingleNode(_findUserXPath, null);
        result.Should().NotBeNull();
        result.Name.Should().Be("user");
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.XPathExpression::Compile(System.String)")]

    [Fact]
    public void GivenAXPathNavigator_WhenSelectExpression_Vulnerable()
    {
        StringReader reader = new StringReader(xmlContent);
        var doc = new XPathDocument(reader);
        var nav = doc.CreateNavigator();
        XPathExpression expression = XPathExpression.Compile(_findUserXPath);
        var result = nav.Select(expression);
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAXPathNavigator_WhenSelectSingleNodeExpression_Vulnerable2()
    {
        StringReader reader = new StringReader(xmlContent);
        var doc = new XPathDocument(reader);
        var nav = doc.CreateNavigator();
        XPathExpression expression = XPathExpression.Compile(_findUserXPath);
        var result = nav.SelectSingleNode(expression);
        result.Should().NotBeNull();
        result.Name.Should().Be("user");
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.XPathExpression::Compile(System.String,System.Xml.IXmlNamespaceResolver)", 1)]

    [Fact]
    public void GivenAXmlNode_WhenSelectSingleNodeXPathExpression_Vulnerable2()
    {
        StringReader reader = new StringReader(xmlContent);
        var doc = new XPathDocument(reader);
        var nav = doc.CreateNavigator();
        XPathExpression expression = XPathExpression.Compile(_findUserXPath, null);
        var result = nav.SelectSingleNode(expression);
        result.Should().NotBeNull();
        result.Name.Should().Be("user");
        AssertVulnerable();
    }

    [Fact]
    public void GivenAXmlNode_WhenSelectXPathExpression_Vulnerable2()
    {
        StringReader reader = new StringReader(xmlContent);
        var doc = new XPathDocument(reader);
        var nav = doc.CreateNavigator();
        XPathExpression expression = XPathExpression.Compile(_findUserXPath, null);
        var result = nav.Select(expression);
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAXmlNode_WhenEvaluateXPathExpression_Vulnerable()
    {
        StringReader reader = new StringReader(xmlContent);
        var doc = new XPathDocument(reader);
        var nav = doc.CreateNavigator();
        XPathExpression expression = XPathExpression.Compile(_findUserXPath, null);
        var result = nav.Evaluate(expression);
        result.Should().NotBeNull();
        ((XPathNodeIterator)result).Count.Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAXmlNode_WhenEvaluateXPathExpression_Vulnerable2()
    {
        StringReader reader = new StringReader(xmlContent);
        var doc = new XPathDocument(reader);
        var nav = doc.CreateNavigator();
        XPathExpression expression = XPathExpression.Compile(_findUserXPath, null);
        var result = nav.Evaluate(expression, null);
        result.Should().NotBeNull();
        ((XPathNodeIterator)result).Count.Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.XPathNavigator::Evaluate(System.String)")]

    [Fact]
    public void GivenAXPathNavigator_WhenEvaluate_Vulnerable()
    {
        StringReader reader = new StringReader(xmlContent);
        var doc = new XPathDocument(reader);
        var nav = doc.CreateNavigator();
        var result = nav.Evaluate(_evaluateExpression);
        result.Should().NotBeNull();
        ((XPathNodeIterator)result).Count.Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.XPathNavigator::Evaluate(System.String,System.Xml.IXmlNamespaceResolver)", 1)]

    [Fact]
    public void GivenAXPathNavigator_WhenEvaluate_Vulnerable2()
    {
        StringReader reader = new StringReader(xmlContent);
        var doc = new XPathDocument(reader);
        var nav = doc.CreateNavigator();
        var result = nav.Evaluate(_evaluateExpression, null);
        result.Should().NotBeNull();
        ((XPathNodeIterator)result).Count.Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.XPathNavigator::SelectSingleNode(System.String)")]

    [Fact]
    public void GivenAXPathNavigator_WhenSelectSingleNode_Vulnerable()
    {
        StringReader reader = new StringReader(xmlContent);
        var doc = new XPathDocument(reader);
        var nav = doc.CreateNavigator();
        var result = nav.SelectSingleNode(_findUserXPath);
        result.Should().NotBeNull();
        result.Name.Should().Be("user");
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.XPathNavigator::SelectSingleNode(System.String,System.Xml.IXmlNamespaceResolver)", 1)]

    [Fact]
    public void GivenAXPathNavigator_WhenSelectSingleNode_Vulnerable2()
    {
        StringReader reader = new StringReader(xmlContent);
        var doc = new XPathDocument(reader);
        var nav = doc.CreateNavigator();
        var result = nav.SelectSingleNode(_findUserXPath, null);
        result.Should().NotBeNull();
        result.Name.Should().Be("user");
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.XPathNavigator::Select(System.String)")]

    [Fact]
    public void GivenAXPathNavigator_WhenSelect_Vulnerable2()
    {
        StringReader reader = new StringReader(xmlContent);
        var doc = new XPathDocument(reader);
        var nav = doc.CreateNavigator();
        var result = nav.Select(_findUserXPath);
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.XPathNavigator::Select(System.String,System.Xml.IXmlNamespaceResolver)", 1)]

    [Fact]
    public void GivenAXPathNavigator_WhenSelect_Vulnerable()
    {
        StringReader reader = new StringReader(xmlContent);
        var doc = new XPathDocument(reader);
        var nav = doc.CreateNavigator();
        var result = nav.Select(_findUserXPath, null);
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.Extensions::XPathEvaluate(System.Xml.Linq.XNode,System.String)")]

    [Fact]
    public void GivenAXElement_WhenEvaluate_Vulnerable()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        XElement root2 = XElement.Load(new XmlNodeReader(doc));
        var result = root2.XPathEvaluate(expression) as IEnumerable;
        result.Cast<object>().Count().Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.Extensions::XPathEvaluate(System.Xml.Linq.XNode,System.String,System.Xml.IXmlNamespaceResolver)", 1)]

    [Fact]
    public void GivenAXElement_WhenEvaluate_Vulnerable2()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        XElement root = XElement.Load(new XmlNodeReader(doc));
        var result = root.XPathEvaluate(expression, null) as IEnumerable;
        result.Cast<object>().Count().Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.Extensions::XPathSelectElement(System.Xml.Linq.XNode,System.String)")]

    [Fact]
    public void GivenAXElement_WhenXPathSelectElement_Vulnerable()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        XElement root = XElement.Load(new XmlNodeReader(doc));
        var result = root.XPathSelectElement(expression);
        result.Name.ToString().Should().Be("user");
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.Extensions::XPathSelectElement(System.Xml.Linq.XNode,System.String,System.Xml.IXmlNamespaceResolver)", 1)]

    [Fact]
    public void GivenAXElement_WhenXPathSelectElement_Vulnerable2()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        XElement root = XElement.Load(new XmlNodeReader(doc));
        var result = root.XPathSelectElement(expression, null);
        result.Name.ToString().Should().Be("user");
        AssertVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.Extensions::XPathSelectElements(System.Xml.Linq.XNode,System.String)")]

    [Fact]
    public void GivenAXElement_WhenXPathSelectElements_Vulnerable()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        XElement root = XElement.Load(new XmlNodeReader(doc));
        var result = root.XPathSelectElements(expression);
        result.Should().NotBeNull();
        result.Count().Should().BeGreaterThan(1);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAXElement_WhenXPathSelectElementsNotTainted_NotVulnerable()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        XElement root = XElement.Load(new XmlNodeReader(doc));
        var result = root.XPathSelectElements("notTainted");
        result.Should().NotBeNull();
        result.Count().Should().Be(0);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAXElement_WhenXPathSelectElementsNull_NotVulnerable()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        XElement root = XElement.Load(new XmlNodeReader(doc));
        try
        {
            root.XPathSelectElements(null);
            Assert.True(false, "XPathException should be thrown");
        }
        catch (XPathException)
        {
        }

        AssertNotVulnerable();
    }

    //[AspectMethodInsertBefore("System.Xml.XPath.Extensions::XPathSelectElements(System.Xml.Linq.XNode,System.String,System.Xml.IXmlNamespaceResolver)", 1)]

    [Fact]
    public void GivenAXElement_WhenXPathSelectElements_Vulnerable2()
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        XElement root2 = XElement.Load(new XmlNodeReader(doc));
        var result = root2.XPathSelectElements(expression, null);
        result.Should().NotBeNull();
        result.Count().Should().BeGreaterThan(1);
        AssertVulnerable();
    }
}
