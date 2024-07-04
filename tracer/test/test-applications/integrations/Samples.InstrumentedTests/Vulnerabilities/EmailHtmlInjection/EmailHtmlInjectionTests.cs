// <copyright file="EmailHtmlInjectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Net.Mail;
using System.Net;
using Xunit;
using System.Web;
using System.Text;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;
public class EmailHtmlInjectionTests : InstrumentationTestsBase
{ 
    private string taintedName = "Alice";
    private string untaintedName = "Peter";
    private string taintedLastName = "Stevens";
    private string untaintedLastName = "Smith";
    private string email = "alice@aliceland.com";
    private string smtpServer = "120.0.0.1";
    private string smtpUsername = "alice@alice.com";
    private int smtpPort = 587;
    private string smtpPassword = "password";
    protected static string emailHtmlInjectionType = "EMAIL_HTML_INJECTION";

    public EmailHtmlInjectionTests()
    {
        AddTainted(taintedName);
        AddTainted(taintedLastName);
        Environment.SetEnvironmentVariable("PATH", "testPath");
    }

    // Tests for method Send(MailMessage message);

    [Fact]
    public void GivenAnEmail_WhenSentTaintedVaulesHtml_ThenIsVulnerable()
    {
        var contentHtml = $"Hi {taintedName} {taintedLastName}!";
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        var client = BuildClient();
        TestEmailSendCall(() => client.Send(mailMessage));
        AssertVulnerable(emailHtmlInjectionType, "Hi :+-Alice-+: :+-Stevens-+:!");
    }

    [Fact]
    public void GivenAnEmail_WhenSentTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        var taintedNameEscaped = WebUtility.HtmlEncode(taintedName);
        var taintedLastNameEscaped = WebUtility.HtmlEncode(taintedLastName);
        var contentHtml = $"Hi {taintedNameEscaped} {taintedLastName}!";
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        var client = BuildClient();
        TestEmailSendCall(() => client.Send(mailMessage));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAnEmail_WhenSentTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        var taintedNameEscaped = HttpUtility.HtmlEncode(taintedName);
        var taintedLastNameEscaped = HttpUtility.HtmlEncode(taintedLastName);
        var contentHtml = $"Hi {taintedNameEscaped} {taintedLastName}!";
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        var client = BuildClient();
        TestEmailSendCall(() => client.Send(mailMessage));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAnEmail_WhenSentNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        var contentHtml = $"Hi {untaintedName} {untaintedLastName}!";
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        var client = BuildClient();
        TestEmailSendCall(() => client.Send(mailMessage));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAnEmail_WhenSentTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        var contentHtml = $"Hi {taintedName} {taintedLastName}!";
        var mailMessage = BuildMailMessage(false, taintedName, taintedLastName);
        var client = BuildClient();
        TestEmailSendCall(() => client.Send(mailMessage));
        AssertNotVulnerable();
    }

    private SmtpClient BuildClient()
    {
        return new SmtpClient(smtpServer, smtpPort)
        {
            Credentials = new NetworkCredential(smtpUsername, smtpPassword),
            EnableSsl = true,
            Timeout = 1
        };
    }

    private MailMessage BuildMailMessage(bool isHtml, string name, string lastName)
    {
        string contentHtml = $"Hi {name} {lastName}!";
        var subject = $"Welcome!";

        var mailMessage = new MailMessage();
        mailMessage.From = new MailAddress(smtpUsername);
        mailMessage.To.Add(email);
        mailMessage.Subject = subject;
        mailMessage.Body = contentHtml;
        mailMessage.IsBodyHtml = isHtml;
        return mailMessage;
    }

    private void TestEmailSendCall(Action expression)
    {
        try
        {
            expression.Invoke();
        }
        catch (SmtpException) { }
    }
}
