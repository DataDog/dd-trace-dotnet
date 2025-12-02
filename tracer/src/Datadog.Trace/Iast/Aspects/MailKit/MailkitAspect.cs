// <copyright file="MailkitAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Text;
using Datadog.Trace.AppSec;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> Email html injection class aspect </summary>
[AspectClass("Mailkit", AspectType.Sink, VulnerabilityType.EmailHtmlInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public sealed class MailkitAspect
{
    /// <summary>
    /// Launches a email html injection vulnerability if the email body is tainted, it's not escaped and the email is html compatible.
    /// We loose the tainted string when setting it to the email body because it's converted to a stream.
    /// Therefore, we cannot check at the moment of sending and we need to instrument the body set text methods.
    /// </summary>
    /// <param name="instance">the email body instance</param>
    /// <param name="encoding">the email encoding</param>
    /// <param name="bodyText">the email text</param>
    [AspectMethodReplace("MimeKit.TextPart::SetText(System.String,System.String)")]
    public static void SetText(object instance, string encoding, string bodyText)
#pragma warning disable DD0005
    {
        IMimeKitTextPart? textPart = ConvertToMimekit(instance);

        if (textPart is not null)
        {
            textPart.SetText(encoding, bodyText);
            CheckForVulnerability(bodyText, textPart);
        }
    }
#pragma warning restore DD0005

    /// <summary>
    /// Launches a email html injection vulnerability if the email body is tainted, it's not escaped and the email is html compatible.
    /// </summary>
    /// <param name="instance">the email body</param>
    /// <param name="encoding">the email ending</param>
    /// <param name="bodyText">the email text</param>
    [AspectMethodReplace("MimeKit.TextPart::SetText(System.Text.Encoding,System.String)")]
    public static void SetTextSystemTextEncoding(object instance, Encoding encoding, string bodyText)
#pragma warning disable DD0005
    {
        IMimeKitTextPart? textPart = ConvertToMimekit(instance);

        if (textPart is not null)
        {
            textPart.SetText(encoding, bodyText);
            CheckForVulnerability(bodyText, textPart);
        }
    }
#pragma warning restore DD0005

    /// <summary>
    /// Launches a email html injection vulnerability if the email body is tainted, it's not escaped and the email is html compatible.
    /// </summary>
    /// <param name="instance">the email body</param>
    /// <param name="bodyText">the email text</param>
    [AspectMethodReplace("MimeKit.TextPart::set_Text(System.String)")]
    public static void SetTextProperty(object instance, string bodyText)
#pragma warning disable DD0005
    {
        IMimeKitTextPart? textPart = ConvertToMimekit(instance);

        if (textPart is not null)
        {
            textPart.Text = bodyText;
            CheckForVulnerability(bodyText, textPart);
        }
    }
#pragma warning restore DD0005

    private static void CheckForVulnerability(string bodyText, IMimeKitTextPart textPart)
    {
        try
        {
            if (textPart.IsHtml)
            {
                IastModule.OnEmailHtmlInjection(bodyText);
            }
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
        }
    }

    private static IMimeKitTextPart? ConvertToMimekit(object instance)
    {
        IMimeKitTextPart? textPart = null;
        try
        {
            textPart = instance.DuckCast<IMimeKitTextPart>();
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex, "(DuckCast)");
        }

        return textPart;
    }
}
