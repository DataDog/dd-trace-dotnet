// <copyright file="SecurityConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec
{
    internal static class SecurityConstants
    {
        internal const string ObfuscationParameterKeyRegexDefault = @"(?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?)key)|token|consumer_?(?:id|key|secret)|sign(?:ed|ature)|bearer|authorization";
        internal const string ObfuscationParameterValueRegexDefault = @"(?i)(?:p(?:ass)?w(?:or)?d|pass(?:[_-]?phrase)?|secret(?:[_-]?key)?|(?:(?:api|private|public|access)[_-]?)key(?:[_-]?id)?|(?:(?:auth|access|id|refresh)[_-]?)?token|consumer[_-]?(?:id|key|secret)|sign(?:ed|ature)?|auth(?:entication|orization)?|jsessionid|phpsessid|asp\.net(?:[_-]|-)sessionid|sid|jwt)(?:\s*=([^;&]+)|""\s*:\s*(""[^""]+""|\d+))|bearer\s+([a-z0-9\._\-]+)|token\s*:\s*([a-z0-9]{13})|gh[opsu]_([0-9a-zA-Z]{36})|ey[I-L][\w=-]+\.(ey[I-L][\w=-]+(?:\.[\w.+\/=-]+)?)|[\-]{5}BEGIN[a-z\s]+PRIVATE\sKEY[\-]{5}([^\-]+)[\-]{5}END[a-z\s]+PRIVATE\sKEY|ssh-rsa\s*([a-z0-9\/\.+]{100,})";

        internal const string SecurityResponseIdPlaceholder = "[security_response_id]";

        internal const string BlockedJsonTemplate = """{"errors":[{"title":"You've been blocked","detail":"Sorry, you cannot access this page. Please contact the customer service team. Security provided by Datadog."}],"security_response_id":"[security_response_id]"}""";
        internal const string BlockedHtmlTemplate = @"<!-- Sorry, you've been blocked -->
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width,initial-scale=1'>
    <title>You've been blocked</title>
    <style>
        a,
        body,
        div,
        html,
        span {
            margin: 0;
            padding: 0;
            border: 0;
            font-size: 100%;
            font: inherit;
            vertical-align: baseline
        }

        body {
            background: -webkit-radial-gradient(26% 19%, circle, #fff, #f4f7f9);
            background: radial-gradient(circle at 26% 19%, #fff, #f4f7f9);
            display: -webkit-box;
            display: -ms-flexbox;
            display: flex;
            -webkit-box-pack: center;
            -ms-flex-pack: center;
            justify-content: center;
            -webkit-box-align: center;
            -ms-flex-align: center;
            align-items: center;
            -ms-flex-line-pack: center;
            align-content: center;
            width: 100%;
            min-height: 100vh;
            line-height: 1;
            flex-direction: column
        }

        p {
            display: block
        }


        main {
            text-align: center;
            flex: 1;
            display: -webkit-box;
            display: -ms-flexbox;
            display: flex;
            -webkit-box-pack: center;
            -ms-flex-pack: center;
            justify-content: center;
            -webkit-box-align: center;
            -ms-flex-align: center;
            align-items: center;
            -ms-flex-line-pack: center;
            align-content: center;
            flex-direction: column
        }

        p {
            font-size: 18px;
            line-height: normal;
            color: #646464;
            font-family: sans-serif;
            font-weight: 400
        }

        a {
            color: #4842b7
        }

        footer {
            width: 100%;
            text-align: center
        }

        footer p {
            font-size: 16px
        }

        .security-response-id {
            font-size:14px;
            color:#999;
            margin-top:20px;
            font-family:monospace
        }
    </style>
</head>

<body>
    <main>
        <p>Sorry, you cannot access this page. Please contact the customer service team.</p>
        <p class='security-response-id'>Security Response ID: [security_response_id]</p>
    </main>
    <footer>
        <p>Security provided by <a
                href='https://www.datadoghq.com/product/security-platform/application-security-monitoring/'
                target='_blank'>Datadog</a></p>
    </footer>
</body>

</html>";
    }
}
