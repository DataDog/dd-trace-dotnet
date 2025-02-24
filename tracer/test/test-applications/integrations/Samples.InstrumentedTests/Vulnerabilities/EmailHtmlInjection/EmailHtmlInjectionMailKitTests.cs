// <copyright file="EmailHtmlInjectionMailKitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using System.Text;
using System.Web;
using FluentAssertions;
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
    [Theory]
    [InlineData(SetTextMode.SetText)]
    [InlineData(SetTextMode.TextProperty)]
    [InlineData(SetTextMode.SetTextEncoding)]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedValuesHtmlEncoding_ThenIsVulnerable(SetTextMode encoding)
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, taintedName, taintedLastName, encoding), default, null), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedSanitizedValuesHtml_ThenIsNotVulnerable()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), default, null), false); 
    }

    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedSanitizedValuesHtml_ThenIsNotVulnerable2()
    {
        TestMailCall(() => new SmtpClient().Send(BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName)), default, null), false);
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

    [Fact]
    public void GivenATextPart_WhenSettingNullValues_ThenArgumentNullExceptionIsThrown()
    {
        var textPart = new TextPart(TextFormat.Html);
        Assert.Throws<ArgumentNullException>(() => textPart.SetText("utf8", null));
        Assert.Throws<ArgumentNullException>(() => textPart.SetText((string) null, null));
        Assert.Throws<ArgumentNullException>(() => textPart.SetText((string)null, "string"));
        Assert.Throws<ArgumentNullException>(() => textPart.SetText(Encoding.UTF8, null));
        Assert.Throws<ArgumentNullException>(() => textPart.SetText((Encoding)null, null));
        Assert.Throws<ArgumentNullException>(() => textPart.SetText((Encoding)null, "string"));
        Assert.Throws<ArgumentNullException>(() => textPart.Text = null);
        AssertNotVulnerable();
    }

    public enum SetTextMode
    {
        TextProperty,
        SetTextEncoding,
        SetText,
    }

    private MimeMessage BuildMailMessage(bool isHtml, string name, string lastName, SetTextMode textMode = SetTextMode.SetText)
    {
        {
            var message = new MimeMessage();
            message.Subject = $"Welcome, {name}!";

            var textPart = new TextPart(isHtml ? TextFormat.Html : TextFormat.Text);

            if (textMode == SetTextMode.TextProperty)
            {
                textPart.SetText("utf8", GetContent(name, lastName));
            }
            else if (textMode == SetTextMode.SetTextEncoding)
            {
                textPart.SetText(Encoding.UTF8, GetContent(name, lastName));
            }
            else
            {
                textPart.Text = GetContent(name, lastName);
            }
            
            textPart.IsHtml.Should().Be(isHtml);
            textPart.Text.Should().Be(GetContent(name, lastName));

            message.Body = textPart;
            return message;
        }
    }
}
