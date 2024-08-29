// <copyright file="HardcodedSecretsAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Analyzers;

internal class HardcodedSecretsAnalyzer : IDisposable
{
    private const int UserStringsArraySize = 100;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<HardcodedSecretsAnalyzer>();
    private static HardcodedSecretsAnalyzer? _instance = null;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly TimeSpan _regexTimeout;
    private List<SecretRegex>? _secretRules = null;

    // Internal for testing
    internal HardcodedSecretsAnalyzer(TimeSpan regexTimeout)
    {
        Log.Debug("HardcodedSecretsAnalyzer -> Init");
        _regexTimeout = regexTimeout;
        Task.Run(() => PollingThread(_cancellationTokenSource.Token))
            .ContinueWith(t => Log.Error(t.Exception, "Error in Hardcoded secret analyzer"), TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task PollingThread(CancellationToken cancellationToken)
    {
        try
        {
            Log.Debug("HardcodedSecretsAnalyzer polling thread -> Started");
            var userStrings = new UserStringInterop[UserStringsArraySize];
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.HardcodedSecret))
                {
                    int userStringLen = NativeMethods.GetUserStrings(userStrings.Length, userStrings);
                    Log.Debug("HardcodedSecretsAnalyzer polling thread -> Retrieved {UserStringLen} strings", userStringLen.ToString());
                    if (userStringLen > 0)
                    {
                        for (int x = 0; x < userStringLen; x++)
                        {
                            try
                            {
                                var value = Marshal.PtrToStringUni(userStrings[x].Value);
                                if (string.IsNullOrEmpty(value))
                                {
                                    continue;
                                }

                                var match = CheckSecret(value);
                                if (!string.IsNullOrEmpty(match))
                                {
                                    var location = Marshal.PtrToStringUni(userStrings[x].Location);
                                    if (string.IsNullOrEmpty(location))
                                    {
                                        Log.Warning("HardcodedSecretsAnalyzer polling thread -> Found {Match} secret with empty (unknown) location", match);
                                        location = "Unknown";
                                    }

                                    Log.Debug("HardcodedSecretsAnalyzer polling thread -> Found {Match} secret", match);
                                    IastModule.OnHardcodedSecret(new Vulnerability(
                                        VulnerabilityTypeName.HardcodedSecret,
                                        (VulnerabilityTypeName.HardcodedSecret + ":" + location! + ":" + match!).GetStaticHashCode(),
                                        new Location(location!),
                                        new Evidence(match!),
                                        IntegrationId.HardcodedSecret));
                                }
                            }
                            catch (Exception err) when (!(err is OperationCanceledException))
                            {
                                Log.Warning(err, "Exception in HardcodedSecretsAnalyzer polling thread loop.");
                            }
                        }

                        if (userStringLen == userStrings.Length)
                        {
                            continue; // Skip wait time if array came full
                        }
                    }
                }

                await Task.Delay(2_000, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception err) when (!(err is OperationCanceledException))
        {
            Log.Warning(err, "Exception in HardcodedSecretsAnalyzer polling thread. Disabling feature.");
        }

        Log.Debug("HardcodedSecretsAnalyzer polling thread -> Exit");
    }

    internal static void Initialize(TimeSpan regexTimeout)
    {
        lock (Log)
        {
            if (_instance == null)
            {
                _instance = new HardcodedSecretsAnalyzer(regexTimeout);
                LifetimeManager.Instance.AddShutdownTask(_ => _instance.Dispose());
            }
        }
    }

    internal string? CheckSecret(string secret)
    {
        if (_secretRules == null)
        {
            _secretRules = GenerateSecretRules(_regexTimeout);
        }

        foreach (var rule in _secretRules)
        {
            try
            {
                if (rule.Regex.IsMatch(secret))
                {
                    return rule.Rule;
                }
            }
            catch (RegexMatchTimeoutException err)
            {
                IastModule.LogTimeoutError(err);
            }
        }

        return null;
    }

    // Rules imported from https://github.com/gitleaks/gitleaks/blob/master/cmd/generate/config/rules
    private static List<SecretRegex> GenerateSecretRules(TimeSpan timeout)
    {
        var res = new List<SecretRegex>(68); // Note: Update this with the new rules count, if modified

        res.Add(new SecretRegex("aws-access-token", @"\b((A3T[A-Z0-9]|AKIA|AGPA|AIDA|AROA|AIPA|ANPA|ANVA|ASIA)[A-Z0-9]{16})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("private-key", @"(?i)-----BEGIN[ A-Z0-9_-]{0,100}PRIVATE KEY( BLOCK)?-----[\s\S-]*KEY( BLOCK)?----", timeout));
        res.Add(new SecretRegex("adobe-client-secret", @"(?i)\b((p8e-)(?i)[a-z0-9]{32})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("age-secret-key", @"AGE-SECRET-KEY-1[QPZRY9X8GF2TVDW0S3JN54KHCE6MUA7L]{58}", timeout));
        res.Add(new SecretRegex("alibaba-access-key-id", @"(?i)\b((LTAI)(?i)[a-z0-9]{20})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("authress-service-client-access-key", @"(?i)\b((?:sc|ext|scauth|authress)_[a-z0-9]{5,30}\.[a-z0-9]{4,6}\.acc[_-][a-z0-9-]{10,32}\.[a-z0-9+/_=-]{30,120})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("clojars-api-token", @"(?i)(CLOJARS_)[a-z0-9]{60}", timeout));
        res.Add(new SecretRegex("databricks-api-token", @"(?i)\b(dapi[a-h0-9]{32})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("digitalocean-pat", @"(?i)\b(dop_v1_[a-f0-9]{64})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("digitalocean-access-token", @"(?i)\b(doo_v1_[a-f0-9]{64})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("digitalocean-refresh-token", @"(?i)\b(dor_v1_[a-f0-9]{64})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("doppler-api-token", @"(dp\.pt\.)(?i)[a-z0-9]{43}", timeout));
        res.Add(new SecretRegex("duffel-api-token", @"duffel_(test|live)_(?i)[a-z0-9_\-=]{43}", timeout));
        res.Add(new SecretRegex("dynatrace-api-token", @"dt0c01\.(?i)[a-z0-9]{24}\.[a-z0-9]{64}", timeout));
        res.Add(new SecretRegex("easypost-api-token", @"\bEZAK(?i)[a-z0-9]{54}", timeout));
        res.Add(new SecretRegex("flutterwave-public-key", @"FLWPUBK_TEST-(?i)[a-h0-9]{32}-X", timeout));
        res.Add(new SecretRegex("frameio-api-token", @"fio-u-(?i)[a-z0-9\-_=]{64}", timeout));
        res.Add(new SecretRegex("gcp-api-key", @"(?i)\b(AIza[0-9A-Za-z\-_]{35})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("github-pat", @"ghp_[0-9a-zA-Z]{36}", timeout));
        res.Add(new SecretRegex("github-fine-grained-pat", @"github_pat_[0-9a-zA-Z_]{82}", timeout));
        res.Add(new SecretRegex("github-oauth", @"gho_[0-9a-zA-Z]{36}", timeout));
        res.Add(new SecretRegex("github-app-token", @"(ghu|ghs)_[0-9a-zA-Z]{36}", timeout));
        res.Add(new SecretRegex("gitlab-pat", @"glpat-[0-9a-zA-Z\-_]{20}", timeout));
        res.Add(new SecretRegex("gitlab-ptt", @"glptt-[0-9a-f]{40}", timeout));
        res.Add(new SecretRegex("gitlab-rrt", @"GR1348941[0-9a-zA-Z\-_]{20}", timeout));
        res.Add(new SecretRegex("grafana-api-key", @"(?i)\b(eyJrIjoi[A-Za-z0-9]{70,400}={0,2})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("grafana-cloud-api-token", @"(?i)\b(glc_[A-Za-z0-9+/]{32,400}={0,2})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("grafana-service-account-token", @"(?i)\b(glsa_[A-Za-z0-9]{32}_[A-Fa-f0-9]{8})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("hashicorp-tf-api-token", @"(?i)[a-z0-9]{14}\.atlasv1\.[a-z0-9\-_=]{60,70}", timeout));
        res.Add(new SecretRegex("jwt", @"\b(ey[a-zA-Z0-9]{17,}\.ey[a-zA-Z0-9\/_-]{17,}\.(?:[a-zA-Z0-9\/_-]{10,}={0,2})?)(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("linear-api-key", @"lin_api_(?i)[a-z0-9]{40}", timeout));
        res.Add(new SecretRegex("npm-access-token", @"(?i)\b(npm_[a-z0-9]{36})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("openai-api-key", @"(?i)\b(sk-[a-zA-Z0-9]{20}T3BlbkFJ[a-zA-Z0-9]{20})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("planetscale-password", @"(?i)\b(pscale_pw_(?i)[a-z0-9=\-_\.]{32,64})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("planetscale-api-token", @"(?i)\b(pscale_tkn_(?i)[a-z0-9=\-_\.]{32,64})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("planetscale-oauth-token", @"(?i)\b(pscale_oauth_(?i)[a-z0-9=\-_\.]{32,64})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("postman-api-token", @"(?i)\b(PMAK-(?i)[a-f0-9]{24}\-[a-f0-9]{34})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("prefect-api-token", @"(?i)\b(pnu_[a-z0-9]{36})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("pulumi-api-token", @"(?i)\b(pul-[a-f0-9]{40})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("pypi-upload-token", @"pypi-AgEIcHlwaS5vcmc[A-Za-z0-9\-_]{50,1000}", timeout));
        res.Add(new SecretRegex("readme-api-token", @"(?i)\b(rdme_[a-z0-9]{70})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("rubygems-api-token", @"(?i)\b(rubygems_[a-f0-9]{48})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("scalingo-api-token", @"tk-us-[a-zA-Z0-9-_]{48}", timeout));
        res.Add(new SecretRegex("sendgrid-api-token", @"(?i)\b(SG\.(?i)[a-z0-9=_\-\.]{66})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("sendinblue-api-token", @"(?i)\b(xkeysib-[a-f0-9]{64}\-(?i)[a-z0-9]{16})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("shippo-api-token", @"(?i)\b(shippo_(live|test)_[a-f0-9]{40})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("shopify-shared-secret", @"shpss_[a-fA-F0-9]{32}", timeout));
        res.Add(new SecretRegex("shopify-access-token", @"shpat_[a-fA-F0-9]{32}", timeout));
        res.Add(new SecretRegex("shopify-custom-access-token", @"shpca_[a-fA-F0-9]{32}", timeout));
        res.Add(new SecretRegex("shopify-private-app-access-token", @"shppa_[a-fA-F0-9]{32}", timeout));
        res.Add(new SecretRegex("sidekiq-sensitive-url", @"(?i)\b(http(?:s??):\/\/)([a-f0-9]{8}:[a-f0-9]{8})@(?:gems.contribsys.com|enterprise.contribsys.com)(?:[\/|\#|\?|:]|$)", timeout));
        res.Add(new SecretRegex("slack-bot-token", @"(xoxb-[0-9]{10,13}\-[0-9]{10,13}[a-zA-Z0-9-]*)", timeout));
        res.Add(new SecretRegex("slack-user-token", @"(xox[pe](?:-[0-9]{10,13}){3}-[a-zA-Z0-9-]{28,34})", timeout));
        res.Add(new SecretRegex("slack-app-token", @"(?i)(xapp-\d-[A-Z0-9]+-\d+-[a-z0-9]+)", timeout));
        res.Add(new SecretRegex("slack-config-access-token", @"(?i)(xoxe.xox[bp]-\d-[A-Z0-9]{163,166})", timeout));
        res.Add(new SecretRegex("slack-config-refresh-token", @"(?i)(xoxe-\d-[A-Z0-9]{146})", timeout));
        res.Add(new SecretRegex("slack-legacy-bot-token", @"(xoxb-[0-9]{8,14}\-[a-zA-Z0-9]{18,26})", timeout));
        res.Add(new SecretRegex("slack-legacy-workspace-token", @"(xox[ar]-(?:\d-)?[0-9a-zA-Z]{8,48})", timeout));
        res.Add(new SecretRegex("slack-legacy-token", @"(xox[os]-\d+-\d+-\d+-[a-fA-F\d]+)", timeout));
        res.Add(new SecretRegex("slack-webhook-url", @"(https?:\/\/)?hooks.slack.com\/(services|workflows)\/[A-Za-z0-9+\/]{43,46}", timeout));
        res.Add(new SecretRegex("square-access-token", @"(?i)\b(sq0atp-[0-9A-Za-z\-_]{22})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("square-secret", @"(?i)\b(sq0csp-[0-9A-Za-z\-_]{43})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("stripe-access-token", @"(?i)(sk|pk)_(test|live)_[0-9a-z]{10,32}", timeout));
        res.Add(new SecretRegex("microsoft-teams-webhook", @"https:\/\/[a-z0-9]+\.webhook\.office\.com\/webhookb2\/[a-z0-9]{8}-([a-z0-9]{4}-){3}[a-z0-9]{12}@[a-z0-9]{8}-([a-z0-9]{4}-){3}[a-z0-9]{12}\/IncomingWebhook\/[a-z0-9]{32}\/[a-z0-9]{8}-([a-z0-9]{4}-){3}[a-z0-9]{12}", timeout));
        res.Add(new SecretRegex("telegram-bot-api-token", @"(?i)(?:^|[^0-9])([0-9]{5,16}:A[a-zA-Z0-9_\-]{34})(?:$|[^a-zA-Z0-9_\-])", timeout));
        res.Add(new SecretRegex("twilio-api-key", @"SK[0-9a-fA-F]{32}", timeout));
        res.Add(new SecretRegex("vault-service-token", @"(?i)\b(hvs\.[a-z0-9_-]{90,100})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));
        res.Add(new SecretRegex("vault-batch-token", @"(?i)\b(hvb\.[a-z0-9_-]{138,212})(?:['|""|\n|\r|\s|\x60|;]|$)", timeout));

        return res;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        Log.Debug("HardcodedSecretsAnalyzer -> Disposed");
    }

    private readonly struct SecretRegex
    {
        public readonly string Rule;
        public readonly Regex Regex;

        public SecretRegex(string rule, string regex, TimeSpan timeout)
        {
            Rule = rule;
            Regex = new Regex(regex, RegexOptions.Compiled, timeout);
        }
    }
}
