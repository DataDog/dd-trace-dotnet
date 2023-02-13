#r "nuget: Newtonsoft.Json, 13.0.1"

open System
open System.Runtime.InteropServices

module Native =
    [<DllImport("ddwaf.so")>]
    extern IntPtr ddwaf_get_version()

module QueryVersions =
    open System
    open System.IO
    open System.Reflection
    open Newtonsoft.Json
    open Newtonsoft.Json.Linq

    let unknownRulesDefault = "1.2.5"
    let assem = Assembly.LoadFrom("/opt/datadog/netcoreapp3.1/Datadog.Trace.dll")

    let writeRulesVersion () =
        let ruleVersion =
            use stream = assem.GetManifestResourceStream("Datadog.Trace.AppSec.Waf.rule-set.json")
            use reader = new StreamReader(stream);
            use jsonReader = new JsonTextReader(reader);
            let root = JToken.ReadFrom(jsonReader);
            let metadata = root.Value<JObject>("metadata");
            if metadata = null then
                unknownRulesDefault
            else
                let ruleVersion = metadata.Value<JValue>("rules_version");
                if ruleVersion = null || ruleVersion.Value = null then
                    unknownRulesDefault
                else
                    ruleVersion.Value.ToString()
        File.WriteAllText("/binaries/APPSEC_EVENT_RULES_VERSION", ruleVersion)

    let writeWafVersion () =
        let buffer = Native.ddwaf_get_version()
        let version = Marshal.PtrToStringAnsi(buffer)
        File.WriteAllText("/binaries/LIBDDWAF_VERSION", version)

    writeRulesVersion ()
    writeWafVersion ()