using System.Net;
using System.Net.Mail;
using MimeKit;
using MailKit.Security;
using Amazon.SimpleEmail.Model;
using Amazon.SimpleEmail;
using System.Collections.Generic;
using Amazon;
using System;

#nullable enable

namespace Samples.Security.AspNetCore5.Helpers;

public struct EmailData
{
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Library { get; set; }
    public string SmtpUsername { get; set; }
    public string SmtpPassword { get; set; }
    public string SmtpServer { get; set; }
    public int SmtpPort { get; set; }
    public bool Escape { get; set; }
}
public static class EmailHelper
{
    public static bool SendMail(EmailData data, string library)
    {
        var contentHtml = $"Hi " + (data.Escape ? WebUtility.HtmlEncode(data.FirstName) : data.FirstName) + " " +
            (data.Escape ? WebUtility.HtmlEncode(data.LastName) : data.LastName) + ", <br />" +
            "We appreciate you subscribing to our newsletter. To complete your subscription, kindly click the link below. <br />" +
            "<a href=\"https://localhost/confirm?token=435345\">Complete your subscription</a>";

        var subject = $"{data.FirstName}, welcome!";

        if (string.IsNullOrEmpty(data.SmtpUsername))
        {
            data.SmtpUsername = data.Email;
        }

        if (library.Equals("mailkit", StringComparison.OrdinalIgnoreCase))
        {
            return SendEmailMailKit(data, contentHtml, subject);
        }
        else if (library.Equals("Awssdk.SimpleMail", StringComparison.OrdinalIgnoreCase))
        {
            return SendEmailAWSSDKSimpleMail(data, contentHtml, subject);
        }
        else if (library.Equals("System.Net.Mail", StringComparison.OrdinalIgnoreCase))
        {
            return SendEmailSystemLib(data, contentHtml, subject);
        }
        else
        {
            return false;
        }
    }

    private static bool SendEmailSystemLib(EmailData data, string contentHtml, string subject)
    {
        try
        {

            var mailMessage = new MailMessage();
            mailMessage.From = new MailAddress(data.SmtpUsername);
            mailMessage.To.Add(data.Email);
            mailMessage.Subject = subject;
            mailMessage.Body = contentHtml;
            mailMessage.IsBodyHtml = true; // Set to true to indicate that the body is HTML

            var client = new System.Net.Mail.SmtpClient(data.SmtpServer, data.SmtpPort)
            {
                Credentials = new NetworkCredential(data.SmtpUsername, data.SmtpPassword),
                EnableSsl = true,
                Timeout = 60000
            };
            client.Send(mailMessage);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return false;
        }

        return true;
    }

    private static bool SendEmailMailKit(EmailData data, string contentHtml, string subject)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("TesterMailkit", data.SmtpUsername));
            message.To.Add(new MailboxAddress("The recipientMailkit", data.Email));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = contentHtml
            };

            message.Body = bodyBuilder.ToMessageBody();

            using (var client = new MailKit.Net.Smtp.SmtpClient())
            {
                client.Connect(data.SmtpServer, data.SmtpPort, SecureSocketOptions.Auto);
                client.Authenticate(data.SmtpUsername, data.SmtpPassword);
                client.Send(message);
                client.Disconnect(true);
            }

            return true;
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return false;
        }
    }

    private static bool SendEmailAWSSDKSimpleMail(EmailData data, string contentHtml, string subject)
    {
        try
        {
            var amazonSimpleEmailServiceClient = new AmazonSimpleEmailServiceClient(
                data.SmtpUsername,
                data.SmtpPassword,
                RegionEndpoint.USEast1
            );

            var sendRequest = new SendEmailRequest
            {
                Source = data.Email,
                Destination = new Destination
                {
                    ToAddresses = new List<string> { data.Email }
                },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Html = new Content
                        {
                            Charset = "UTF-8",
                            Data = contentHtml
                        }
                    }
                }
            };

            var response = amazonSimpleEmailServiceClient.SendEmailAsync(sendRequest).Result;

            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return false;
        }
    }
}
