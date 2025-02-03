// <copyright file="EmailHtmlInjectionMailKitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using System.Web;
using MailKit.Net.Smtp;
using MimeKit;
using MimeKit.Text;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class EmailHtmlInjectionMailKitTests : EmailInjectionBaseTests
{
    private MailboxAddress mailBoxSender = new MailboxAddress("sender", "address");
    private MailboxAddress mailBoxRecipient = new MailboxAddress("recipient", "address");

    // Test string Send(MimeMessage message, CancellationToken cancellationToken, ITransferProgress progress)
    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedValuesHtml_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, taintedName, taintedLastName), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedSanitizedValuesHtml_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedSanitizedValuesHtml_ThenIsVulnerable2()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName)), default, null), true);
    }


    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedValuesText_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(false, taintedName, taintedLastName), default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedValuesHtml_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, untaintedName, untaintedLastName), default, null), false);
    }

    // Test Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken, ITransferProgress progress)

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedValuesHtmlAsync_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(true, taintedName, taintedLastName), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedSanitizedValuesHtmlAsync_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedValuesTextAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(false, taintedName, taintedLastName), default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedValuesHtmlAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(true, untaintedName, untaintedLastName), default, null), false);
    }

    // Test string Send (MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedValuesHtmlWithSender_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, taintedName, taintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedSanitizedValuesHtmlWithSender_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), mailBoxSender, new[] { mailBoxRecipient }, default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedValuesTextWithSender_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(false, taintedName, taintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedValuesHtmlWithSender_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, untaintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    // Test Task<string> SendAsync (MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedValuesHtmlWithSenderAsync_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(true, taintedName, taintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedSanitizedValuesHtmlWithSenderAsync_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), mailBoxSender, new[] { mailBoxRecipient }, default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedValuesTextWithSenderAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(false, taintedName, taintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedValuesHtmlWithSenderAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(true, untaintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    // Test string Send (FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null);

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedValuesHtmlWithOptions_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(true, taintedName, taintedLastName), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedSanitizedValuesHtmlWithOptions_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedValuesTextWithOptions_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(false, taintedName, taintedLastName), default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedValuesHtmlWithOptions_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(true, untaintedName, untaintedLastName), default, null), false);
    }

    // Test Task<string> SendAsync (FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null);

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedValuesHtmlWithOptionsAsync_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(true, taintedName, taintedLastName), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedSanitizedValuesHtmlWithOptionsAsync_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedValuesTextWithOptionsAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(false, taintedName, taintedLastName), default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedValuesHtmlWithOptionsAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(true, untaintedName, untaintedLastName), default, null), false);
    }

    // Test string Send (FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedValuesHtmlWithOptionsWithSender_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(true, taintedName, taintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedSanitizedValuesHtmlWithOptionsWithSender_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), mailBoxSender, new[] { mailBoxRecipient }, default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedValuesTextWithOptionsWithSender_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(false, taintedName, taintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedValuesHtmlWithOptionsWithSender_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(true, untaintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    // Test Task<string> SendAsync (FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedValuesHtmlWithOptionsWithSenderAsync_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(true, taintedName, taintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedSanitizedValuesHtmlWithOptionsWithSenderAsync_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), mailBoxSender, new[] { mailBoxRecipient }, default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedValuesTextWithOptionsWithSenderAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(false, taintedName, taintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedValuesHtmlWithOptionsWithSenderAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(true, untaintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    private MimeMessage BuildMailMessage(bool isHtml, string name, string lastName)
    {
        {
            var message = new MimeMessage();
            message.Subject = "Welcome!";
            message.Body = new TextPart(isHtml ? TextFormat.Html : TextFormat.Text)
            {
                Text = GetContent(name, lastName)
            };

            return message;
        }
    }
}
