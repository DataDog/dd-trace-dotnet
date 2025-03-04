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
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, taintedName, taintedLastName)), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName))), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName))), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailMessageNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, untaintedName, untaintedLastName)), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailMessageTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(false, taintedName, taintedLastName)), false);
    }

    // Tests for method SendMailAsync(MailMessage message);

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageTaintedVaulesHtml_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendMailAsync(BuildMailMessage(true, taintedName, taintedLastName)), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendMailAsync(BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName))), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        TestMailCall(() => new SmtpClient().SendMailAsync(BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName))), false);
    }

    [Fact]
    public void GivenAnEmail_WhenMailSendAsyncMailMessageNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendMailAsync(BuildMailMessage(true, untaintedName, untaintedLastName)), false);
    }

    [Fact]
    public void GivenAnEmail_WhenMailSendAsyncMailMessageTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendMailAsync(BuildMailMessage(false, taintedName, taintedLastName)), false);
    }

    // Test SendAsync(MailMessage message, object userToken);

    [Fact]
    public void GivenAnEmail_WhenSendAsyncMailMessageTaintedVaulesHtml_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(true, taintedName, taintedLastName), null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName)), null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncMailMessageNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(true, untaintedName, untaintedLastName), null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncMailMessageTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(false, taintedName, taintedLastName), null), false);
    }

    // Test public Task SendMailAsync(MailMessage message, CancellationToken cancellationToken)

#if NET5_0_OR_GREATER

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationTaintedVaulesHtml_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendMailAsync(BuildMailMessage(true, taintedName, taintedLastName), default), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendMailAsync(BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), default), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        TestMailCall(() => new SmtpClient().SendMailAsync(BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName)), default), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationNotTaintedVaulesHtml_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendMailAsync(BuildMailMessage(true, untaintedName, untaintedLastName), default), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendMailAsyncMailMessageCancellationTaintedVaulesNoHtml_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendMailAsync(BuildMailMessage(false, taintedName, taintedLastName), default), false);
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
