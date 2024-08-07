// <copyright file="SmtpClientAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> Email html injection class aspect </summary>
[AspectClass("System,System.Net.Mail", AspectType.Sink, VulnerabilityType.EmailHtmlInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class SmtpClientAspect
{
    /// <summary>
    /// Launches a email html injection vulnerability if the email body is tainted, it's not escaped and the email is html compatible.
    /// No need to instrument methods Send(string from, string recipients, string subject, string body) and similar
    /// since those methods would send the email as plain text.
    /// </summary>
    /// <param name="message">the email message that is going to be sent</param>
    /// <returns>the MailMessage</returns>
    [AspectMethodInsertBefore("System.Net.Mail.SmtpClient::Send(System.Net.Mail.MailMessage)")]
    [AspectMethodInsertBefore("System.Net.Mail.SmtpClient::SendAsync(System.Net.Mail.MailMessage,System.Object)", 1)]
    [AspectMethodInsertBefore("System.Net.Mail.SmtpClient::SendMailAsync(System.Net.Mail.MailMessage)")]
#if NETCOREAPP3_1_OR_GREATER
    [AspectMethodInsertBefore("System.Net.Mail.SmtpClient::SendMailAsync(System.Net.Mail.MailMessage,System.Threading.CancellationToken)", 1)]
#endif
    public static object? Send(object? message)
    {
        try
        {
            IastModule.OnEmailHtmlInjection(message);
            return message;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(SmtpClientAspect)}.{nameof(Send)}");
            return message;
        }
    }
}
