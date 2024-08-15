// <copyright file="EmailHtmlInjectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net.Mail;
using System.Net;
using Xunit;
using System.Web;
using System.Threading.Tasks;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class EmailHtmlInjectionTests : InstrumentationTestsBase
{ 
    private static string taintedName = "Alice<h1>Hi</h1>";
    private static string untaintedName = "Peter";
    private static string taintedLastName = "Stevens";
    private static string untaintedLastName = "Smith";
    private static string email = "alice@aliceland.com";
    private static string smtpServer = "127.0.0.1";
    private static string smtpUsername = "alice@alice.com";
    private static int smtpPort = 587;
    private static string smtpPassword = "password";
    protected static string emailHtmlInjectionType = "EMAIL_HTML_INJECTION";

    public EmailHtmlInjectionTests()
    {
        AddTainted(taintedName);
        AddTainted(taintedLastName);
    }

    // Tests for method Send(MailMessage message);

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailMessageTaintedVaulesHtml_ThenIsVulnerable()
    {
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        TestEmailSendCall(() => Send(mailMessage));
        AssertVulnerable(emailHtmlInjectionType, "Hi :+-Alice<h1>Hi</h1>-+: :+-Stevens-+:!");
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName));
        TestEmailSendCall(() => Send(mailMessage));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        var mailMessage = BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName));
        TestEmailSendCall(() => Send(mailMessage));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailMessageNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, untaintedName, untaintedLastName);
        TestEmailSendCall(() => Send(mailMessage));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailMessageTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(false, taintedName, taintedLastName);
        TestEmailSendCall(() => Send(mailMessage));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailMessageNull_ThenIsNotVulnerable()
    {
        try
        {
            Send(null);
        }
        catch (ArgumentNullException) { }
        
        AssertNotVulnerable();
    }

    private void Send(MailMessage mailMessage)
    {
        using (var client = BuildClient())
        {
            client.Send(mailMessage);
        }
    }

    // Tests for method SendMailAsync(MailMessage message);

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageTaintedVaulesHtml_ThenIsVulnerable()
    {
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        TestEmailSendCall(() => SendMailAsync(mailMessage));
        AssertVulnerable(emailHtmlInjectionType, "Hi :+-Alice<h1>Hi</h1>-+: :+-Stevens-+:!");
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName));
        TestEmailSendCall(() => SendMailAsync(mailMessage));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        var mailMessage = BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName));
        TestEmailSendCall(() => SendMailAsync(mailMessage));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenMailSendAsyncMailMessageNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, untaintedName, untaintedLastName);
        TestEmailSendCall(() => SendMailAsync(mailMessage));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenMailSendAsyncMailMessageTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(false, taintedName, taintedLastName);
        TestEmailSendCall(() => SendMailAsync(mailMessage));
        AssertNotVulnerable();
    }

    private void SendMailAsync(MailMessage mailMessage)
    {
        using (var client = BuildClient())
        {
            client.SendMailAsync(mailMessage);
        }
    }

    // Test SendAsync(MailMessage message, object userToken);

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendAsyncMailMessageTaintedVaulesHtml_ThenIsVulnerable()
    {
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        TestEmailSendCall(() => SendAsync(mailMessage, null));
        AssertVulnerable(emailHtmlInjectionType, "Hi :+-Alice<h1>Hi</h1>-+: :+-Stevens-+:!");
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendAsyncMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName));
        TestEmailSendCall(() => SendAsync(mailMessage, null));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendAsyncMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        var mailMessage = BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName));
        TestEmailSendCall(() => SendAsync(mailMessage, null));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendAsyncMailMessageNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, untaintedName, untaintedLastName);
        TestEmailSendCall(() => SendAsync(mailMessage, null));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendAsyncMailMessageTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(false, taintedName, taintedLastName);
        TestEmailSendCall(() => SendAsync(mailMessage, null));
        AssertNotVulnerable();
    }

    private void SendAsync(MailMessage mailMessage, object token)
    {
        using (var client = BuildClient())
        {
            client.SendAsync(mailMessage, token);
        }
    }

    // Test public Task SendMailAsync(MailMessage message, CancellationToken cancellationToken)

#if NET5_0_OR_GREATER

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationTaintedVaulesHtml_ThenIsVulnerable()
    {
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        TestEmailSendCall(() => SendMailAsync(mailMessage, new System.Threading.CancellationToken()));
        AssertVulnerable(emailHtmlInjectionType, "Hi :+-Alice<h1>Hi</h1>-+: :+-Stevens-+:!");
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName));
        TestEmailSendCall(() => SendMailAsync(mailMessage, new System.Threading.CancellationToken()));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        var mailMessage = BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName));
        TestEmailSendCall(() => SendMailAsync(mailMessage, new System.Threading.CancellationToken()));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, untaintedName, untaintedLastName);
        TestEmailSendCall(() => SendMailAsync(mailMessage, new System.Threading.CancellationToken()));
        AssertNotVulnerable();
    }

    [Fact(Skip = "Tests are hanging the CI in netcore <=3.1")]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(false, taintedName, taintedLastName);
        TestEmailSendCall(() => SendMailAsync(mailMessage, new System.Threading.CancellationToken()));
        AssertNotVulnerable();
    }

    private void SendMailAsync(MailMessage mailMessage, System.Threading.CancellationToken cancellationToken)
    {
        using (var client = BuildClient())
        {
            client.SendMailAsync(mailMessage, cancellationToken);
        }
    }

#endif

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
        var contentHtml = GetContent(name, lastName);
        var subject = "Welcome!";

        var mailMessage = new MailMessage();
        mailMessage.From = new MailAddress(smtpUsername);
        mailMessage.To.Add(email);
        mailMessage.Subject = subject;
        mailMessage.Body = contentHtml;
        mailMessage.IsBodyHtml = isHtml;
        return mailMessage;
    }

    private string GetContent(string name, string lastName)
    {
        return "Hi " + name + " " + lastName + "!";
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
