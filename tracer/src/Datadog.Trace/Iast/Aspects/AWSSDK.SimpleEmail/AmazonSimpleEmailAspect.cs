// <copyright file="AmazonSimpleEmailAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> Email html injection class aspect </summary>
[AspectClass("AWSSDK.SimpleEmail", AspectType.Sink, VulnerabilityType.EmailHtmlInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public sealed class AmazonSimpleEmailAspect
{
    /// <summary>
    /// Launches a email html injection vulnerability if the email body is tainted, it's not escaped and the email is html compatible.
    /// </summary>
    /// <param name="message">the email message that is going to be sent</param>
    /// <returns>the MailMessage</returns>
#if NETFRAMEWORK
    [AspectMethodInsertBefore("Amazon.SimpleEmail.AmazonSimpleEmailServiceClient::SendEmail(Amazon.SimpleEmail.Model.SendEmailRequest)")]
#endif
    [AspectMethodInsertBefore("Amazon.SimpleEmail.AmazonSimpleEmailServiceClient::SendEmailAsync(Amazon.SimpleEmail.Model.SendEmailRequest,System.Threading.CancellationToken)", 1)]
    public static object? Send(object? message)
    {
        try
        {
            IastModule.OnEmailHtmlInjection(message, EmailInjectionType.AmazonSimpleEmail);
            return message;
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
            return message;
        }
    }
}
