// <copyright file="SmtpClientAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Net.Mail;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> Xpath injection class aspect </summary>
[AspectClass("System,System.Net.Mail", AspectType.Sink, VulnerabilityType.EmailHtmlInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class SmtpClientAspect
{
    /// <summary>
    /// Launches a xpath injection vulnerability if the input is tainted
    /// </summary>
    /// <param name="message">the email message that is going to be sent</param>
    /// <returns>the path parameter</returns>
    [AspectMethodInsertBefore("System.Net.Mail::Send(System.Net.Mail.MailMessage message)")]
    public static MailMessage ReviewPath(MailMessage message)
    {
        IastModule.OnEmailHtmlInjection(message);
        return message;
    }
}
