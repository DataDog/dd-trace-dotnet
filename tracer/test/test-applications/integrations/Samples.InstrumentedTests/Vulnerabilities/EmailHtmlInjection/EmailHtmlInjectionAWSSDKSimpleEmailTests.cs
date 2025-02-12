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
using System;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class EmailHtmlInjectionAWSSDKSimpleEmailTests : EmailInjectionBaseTests
{
    private MailboxAddress mailBoxSender = new MailboxAddress("sender", "address");
    private MailboxAddress mailBoxRecipient = new MailboxAddress("recipient", "address");

    // Test Task<SendEmailResponse> SendEmailAsync(SendEmailRequest request, System.Threading.CancellationToken cancellationToken = default(CancellationToken))

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageAsyncTaintedVaulesHtml_ThenIsVulnerable()
    {
        TestMailCall(() => new AmazonSimpleEmailServiceClient(Amazon.RegionEndpoint.APEast1).SendEmailAsync(BuildMailMessage(true, taintedName, taintedLastName), default), true);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageAsyncTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        TestMailCall(() => new AmazonSimpleEmailServiceClient(Amazon.RegionEndpoint.APEast1).SendEmailAsync(BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), default), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageAsyncTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        TestMailCall(() => new AmazonSimpleEmailServiceClient(Amazon.RegionEndpoint.APEast1).SendEmailAsync(BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName)), default), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncTextMailMessageTaintedVaulesText_ThenIsNotVulnerable()
    {
        TestMailCall(() => new AmazonSimpleEmailServiceClient(Amazon.RegionEndpoint.APEast1).SendEmailAsync(BuildMailMessage(false, taintedName, taintedLastName), default), false);
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageAsyncNotTainted_ThenIsNotVulnerable()
    {
        TestMailCall(() => new AmazonSimpleEmailServiceClient(Amazon.RegionEndpoint.APEast1).SendEmailAsync(BuildMailMessage(true, untaintedName, untaintedLastName), default), false);
    }

    [Fact]
    public async System.Threading.Tasks.Task GivenATextPart_WhenSettingNullValues_ThenArgumentNullExceptionIsThrown()
    {
        await Assert.ThrowsAsync<NullReferenceException>(() => new AmazonSimpleEmailServiceClient(Amazon.RegionEndpoint.APEast1).SendEmailAsync(null, default));
        AssertNotVulnerable();
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
                Body = new Body()
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
