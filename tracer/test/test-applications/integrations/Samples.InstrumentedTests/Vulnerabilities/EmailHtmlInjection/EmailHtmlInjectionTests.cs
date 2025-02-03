// <copyright file="EmailHtmlInjectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net.Mail;
using System.Net;
using Xunit;
using System.Web;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class EmailHtmlInjectionTests : EmailInjectionBaseTests
{ 
    // Tests for method Send(MailMessage message);

    [Fact]
    public void GivenAnEmail_WhenSendMailMessageTaintedVaulesHtml_ThenIsVulnerable()
    {
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        TestMailCall(() => new SmtpClient().Send(mailMessage), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName));
        TestMailCall(() => new SmtpClient().Send(mailMessage), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        var mailMessage = BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName));
        TestMailCall(() => new SmtpClient().Send(mailMessage), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailMessageNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, untaintedName, untaintedLastName);
        TestMailCall(() => new SmtpClient().Send(mailMessage), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailMessageTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(false, taintedName, taintedLastName);
        TestMailCall(() => new SmtpClient().Send(mailMessage), false);
    }

    // Tests for method SendMailAsync(MailMessage message);

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageTaintedVaulesHtml_ThenIsVulnerable()
    {
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        TestMailCall(() => new SmtpClient().SendMailAsync(mailMessage), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName));
        TestMailCall(() => new SmtpClient().SendMailAsync(mailMessage), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        var mailMessage = BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName));
        TestMailCall(() => new SmtpClient().SendMailAsync(mailMessage), false);
    }

    [Fact]
    public void GivenAnEmail_WhenMailSendAsyncMailMessageNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, untaintedName, untaintedLastName);
        TestMailCall(() => new SmtpClient().SendMailAsync(mailMessage), false);
    }

    [Fact]
    public void GivenAnEmail_WhenMailSendAsyncMailMessageTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(false, taintedName, taintedLastName);
        TestMailCall(() => new SmtpClient().SendMailAsync(mailMessage), false);
    }

    // Test SendAsync(MailMessage message, object userToken);

    [Fact]
    public void GivenAnEmail_WhenSendAsyncMailMessageTaintedVaulesHtml_ThenIsVulnerable()
    {
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        TestMailCall(() => new SmtpClient().SendAsync(mailMessage, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName));
        TestMailCall(() => new SmtpClient().SendAsync(mailMessage, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        var mailMessage = BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName));
        TestMailCall(() => new SmtpClient().SendAsync(mailMessage, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncMailMessageNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, untaintedName, untaintedLastName);
        TestMailCall(() => new SmtpClient().SendAsync(mailMessage, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncMailMessageTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(false, taintedName, taintedLastName);
        TestMailCall(() => new SmtpClient().SendAsync(mailMessage, null), false);
    }

    // Test public Task SendMailAsync(MailMessage message, CancellationToken cancellationToken)

#if NET5_0_OR_GREATER

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationTaintedVaulesHtml_ThenIsVulnerable()
    {
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        TestMailCall(() => new SmtpClient().SendMailAsync(mailMessage, default), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName));
        TestMailCall(() => new SmtpClient().SendMailAsync(mailMessage, default), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        var mailMessage = BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName));
        TestMailCall(() => new SmtpClient().SendMailAsync(mailMessage, default), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, untaintedName, untaintedLastName);
        TestMailCall(() => new SmtpClient().SendMailAsync(mailMessage, default), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(false, taintedName, taintedLastName);
        TestMailCall(() => new SmtpClient().SendMailAsync(mailMessage, default), false);
    }

#endif

    private MailMessage BuildMailMessage(bool isHtml, string name, string lastName)
    {
        var contentHtml = GetContent(name, lastName);
        var subject = "Welcome!";

        var mailMessage = new MailMessage();
        // Not setting the MailMessage To/From properties will throw an exception when sending without going further
        mailMessage.Subject = subject;
        mailMessage.Body = contentHtml;
        mailMessage.IsBodyHtml = isHtml;
        return mailMessage;
    }
}
