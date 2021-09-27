// <copyright file="Waf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.AppSec.Waf.Rules;
using Datadog.Trace.AppSec.Waf.RuleSetJson;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Waf : IWaf
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Waf));

        private readonly RuleSet ruleSet;
        private readonly List<Rule> rules;

        private Waf(RuleSet ruleSet)
        {
            this.ruleSet = ruleSet;
            this.rules = new List<Rule>();
            foreach (var e in ruleSet.Events)
            {
                rules.Add(new Rule(e.Id, e.Name, e.Conditions, e.Transformers));
            }
        }

        // null rulesFile means use rules embedded in the manifest
        public static Waf Initialize(string rulesFile)
        {
            try
            {
                using var stream = GetRulesStream(rulesFile);

                if (stream == null)
                {
                    return null;
                }

                var jsonSerializer = new JsonSerializer();
                using var streamReader = new StreamReader(stream);
                using var jsonReader = new JsonTextReader(streamReader);
                var ruleFile = (RuleSet)jsonSerializer.Deserialize(jsonReader, typeof(RuleSet));

                return new Waf(ruleFile);
            }
            catch (Exception ex)
            {
                if (rulesFile != null)
                {
                    Log.Error(ex, "AppSec could not read the rule file \"{RulesFile}\" as it was invalid. AppSec will not run any protections in this application.", rulesFile);
                }
                else
                {
                    Log.Error(ex, "AppSec could not read the rule file embedded in the manifest as it was invalid. AppSec will not run any protections in this application.");
                }

                return null;
            }
        }

        public IContext CreateContext()
        {
            return new Context(rules);
        }

        private static Stream GetRulesManifestStream()
        {
            var assembly = typeof(Waf).Assembly;
            return assembly.GetManifestResourceStream("Datadog.Trace.AppSec.Waf.rule-set.json");
        }

        private static Stream GetRulesFileStream(string rulesFile)
        {
            if (!File.Exists(rulesFile))
            {
                Log.Error("AppSec could not find the rules file in path \"{RulesFile}\". AppSec will not run any protections in this application.", rulesFile);
                return null;
            }

            return File.OpenRead(rulesFile);
        }

        private static void LogRuleDetailsIfDebugEnabled(JToken root)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                try
                {
                    var eventsProp = root.Value<JArray>("events");
                    foreach (var ev in eventsProp)
                    {
                        var idProp = ev.Value<JValue>("id");
                        var nameProp = ev.Value<JValue>("name");
                        var addresses = ev.Value<JArray>("conditions").SelectMany(x => x.Value<JObject>("parameters").Value<JArray>("inputs"));
                        Log.Debug("Loaded rule: {id} - {name} on addresses: {addresses}", idProp.Value, nameProp.Value, string.Join(", ", addresses));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error occured logging the ddwaf rules");
                }
            }
        }

        private static Stream GetRulesStream(string rulesFile)
        {
            return string.IsNullOrWhiteSpace(rulesFile) ?
                    GetRulesManifestStream() :
                    GetRulesFileStream(rulesFile);
        }
    }
}
