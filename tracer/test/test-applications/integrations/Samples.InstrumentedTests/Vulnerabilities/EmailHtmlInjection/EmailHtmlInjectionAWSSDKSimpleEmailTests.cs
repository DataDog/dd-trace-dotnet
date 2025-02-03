// <copyright file="EmailHtmlInjectionAWSSDKSimpleEmailTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;
using MimeKit;
using Xunit;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using System.Web;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class EmailHtmlInjectionAWSSDKSimpleEmailTests : EmailInjectionBaseTests
{
    private MailboxAddress mailBoxSender = new MailboxAddress("sender", "address");
    private MailboxAddress mailBoxRecipient = new MailboxAddress("recipient", "address");

    // Test Task<SendEmailResponse> SendEmailAsync(SendEmailRequest request, System.Threading.CancellationToken cancellationToken = default(CancellationToken))

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageAsyncTaintedVaulesHtml_ThenIsVulnerable()
    {
        var mailMessage = BuildMailMessage(true, taintedName, taintedLastName);
        TestMailCall(() => new AmazonSimpleEmailServiceClient().SendEmailAsync(mailMessage, default), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageAsyncTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName));
        TestMailCall(() => new AmazonSimpleEmailServiceClient().SendEmailAsync(mailMessage, default), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageAsyncTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        var mailMessage = BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName));
        TestMailCall(() => new AmazonSimpleEmailServiceClient().SendEmailAsync(mailMessage, default), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncTextMailMessageTaintedVaulesText_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(false, taintedName, taintedLastName);
        TestMailCall(() => new AmazonSimpleEmailServiceClient().SendEmailAsync(mailMessage, default), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageAsyncNotTainted_ThenIsNotVulnerable()
    {
        var mailMessage = BuildMailMessage(true, untaintedName, untaintedLastName);
        TestMailCall(() => new AmazonSimpleEmailServiceClient().SendEmailAsync(mailMessage, default), false);
    }

    private SendEmailRequest BuildMailMessage(bool isHtml, string name, string lastName)
    {
        var contentHtml = GetContent(name, lastName);
        var subject = "Welcome!";

        var sendRequest = new SendEmailRequest
        {
            Destination = new Destination
            {
                ToAddresses = new()
            },
            Message = new Message
            {
                Subject = new Content(subject),
                Body = new Body
                {
                }
            }
        };

        if (isHtml)
        {
            sendRequest.Message.Body.Html = new Content
            {
                Charset = "UTF-8",
                Data = contentHtml
            };
        }
        else
        {
            sendRequest.Message.Body.Text = new Content
            {
                Charset = "UTF-8",
                Data = contentHtml
            };
        }

        return sendRequest;
    }
}
