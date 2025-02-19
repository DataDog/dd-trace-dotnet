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
using Amazon.Runtime;
using Moq;
using System.Reflection;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;
using System.Threading;
using Amazon;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class EmailHtmlInjectionAWSSDKSimpleEmailTests : EmailInjectionBaseTests
{
    private MailboxAddress mailBoxSender = new MailboxAddress("sender", "address");
    private MailboxAddress mailBoxRecipient = new MailboxAddress("recipient", "address");

    // Test Task<SendEmailResponse> SendEmailAsync(SendEmailRequest request, System.Threading.CancellationToken cancellationToken = default(CancellationToken))

    public class ClientMock: AmazonSimpleEmailServiceClient
    {
        public ClientMock()
        { }
    }

    public class MyConfiguration: AmazonSimpleEmailServiceConfig
    {
        public override void Validate()
        {
            
        }
    }

#if NETFRAMEWORK
    [Fact]
    public void GivenAnEmail_WhenSendHtmlMailMessageTaintedVaulesHtml_ThenIsVulnerable()
    {
        try
        {
            // The constructor of AmazonSimpleEmailServiceClient makes http calls that can make the test flaky and slow
            // We can still test the aspect with a null object
            ((AmazonSimpleEmailServiceClient)null).SendEmail(BuildMailMessage(true, taintedName, taintedLastName));
        }
        catch
        {
        }
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageTaintedVaulesHtmlNull_ThenIsVulnerable()
    {
        try
        {
            // The constructor of AmazonSimpleEmailServiceClient makes http calls that can make the test flaky and slow
            // We can still test the aspect with a null object
            ((AmazonSimpleEmailServiceClient)null).SendEmailAsync(BuildMailMessage(true, taintedName, taintedLastName), default);
        }
        catch
        {
        }
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageTaintedVaulesHtml_ThenIsVulnerable()
    {
        try
        {
            // The constructor of AmazonSimpleEmailServiceClient makes http calls that can make the test flaky and slow
            // We can still test the aspect with a null object
            ((AmazonSimpleEmailServiceClient)null).SendEmailAsync(BuildMailMessage(true, taintedName, taintedLastName), default);
        }
        catch
        {
        }
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable()
    {
        try
        {
            ((AmazonSimpleEmailServiceClient)null).SendEmailAsync(BuildMailMessage(true, WebUtility.HtmlEncode(taintedName), WebUtility.HtmlEncode(taintedLastName)), default);
        }
        catch
        {
        }
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageTaintedVaulesHtmlEscaped_ThenIsNotVulnerable2()
    {
        try
        {
            ((AmazonSimpleEmailServiceClient)null).SendEmailAsync(BuildMailMessage(true, HttpUtility.HtmlEncode(taintedName), HttpUtility.HtmlEncode(taintedLastName)), default);
        }
        catch
        {
        }
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncTextMailMessageTaintedVaulesText_ThenIsNotVulnerable()
    {
        try
        {
            ((AmazonSimpleEmailServiceClient)null).SendEmailAsync(BuildMailMessage(false, taintedName, taintedLastName), default);
        }
        catch
        {
        }
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAnEmail_WhenSendAsyncHtmlMailMessageEmptyMessage_ThenIsNotVulnerable()
    {
        try
        {
            var sendRequest = new SendEmailRequest
            {
                Destination = new Destination
                {
                    ToAddresses = new()
                },
            };

            ((AmazonSimpleEmailServiceClient)null).SendEmailAsync(sendRequest, default);
        }
        catch
        {
        }
        AssertNotVulnerable();
    }

    private static SendEmailRequest BuildMailMessage(bool isHtml, string name, string lastName)
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
