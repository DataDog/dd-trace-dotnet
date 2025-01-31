// <copyright file="EmailHtmlInjectionMailKitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedVaulesHtml_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, taintedName, untaintedLastName), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedVaulesText_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(false, taintedName, untaintedLastName), default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedVaulesHtml_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(false, untaintedName, untaintedLastName), default, null), false);
    }

    // Test Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken, ITransferProgress progress)

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedVaulesHtmlAsync_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(true, taintedName, untaintedLastName), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedVaulesTextAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(false, taintedName, untaintedLastName), default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedVaulesHtmlAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(false, untaintedName, untaintedLastName), default, null), false);
    }

    // Test string Send (MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedVaulesHtmlWithSender_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, taintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedVaulesTextWithSender_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(false, taintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedVaulesHtmlWithSender_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(false, untaintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    // Test Task<string> SendAsync (MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedVaulesHtmlWithSenderAsync_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(true, taintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedVaulesTextWithSenderAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(false, taintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedVaulesHtmlWithSenderAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(BuildMailMessage(false, untaintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    // Test string Send (FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null);
    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedVaulesHtmlWithOptions_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(true, taintedName, untaintedLastName), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedVaulesTextWithOptions_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(false, taintedName, untaintedLastName), default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedVaulesHtmlWithOptions_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(false, untaintedName, untaintedLastName), default, null), false);
    }

    // Test Task<string> SendAsync (FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null);

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedVaulesHtmlWithOptionsAsync_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(true, taintedName, untaintedLastName), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedVaulesTextWithOptionsAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(false, taintedName, untaintedLastName), default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedVaulesHtmlWithOptionsAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(false, untaintedName, untaintedLastName), default, null), false);
    }

    // Test string Send (FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedVaulesHtmlWithOptionsWithSender_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(true, taintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedVaulesTextWithOptionsWithSender_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(false, taintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedVaulesHtmlWithOptionsWithSender_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(FormatOptions.Default, BuildMailMessage(false, untaintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    // Test Task<string> SendAsync (FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedVaulesHtmlWithOptionsWithSenderAsync_ThenIsVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(true, taintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendTextMailMessageTaintedVaulesTextWithOptionsWithSenderAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(false, taintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageUntaintedVaulesHtmlWithOptionsWithSenderAsync_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().SendAsync(FormatOptions.Default, BuildMailMessage(false, untaintedName, untaintedLastName), mailBoxSender, new[] { mailBoxRecipient }, default, null), false);
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
