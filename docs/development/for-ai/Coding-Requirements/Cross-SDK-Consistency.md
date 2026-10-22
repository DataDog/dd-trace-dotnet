# Cross-SDK consistency

Behaviour that must match other Datadog SDKs, specs (including OpenTelemetry), and the agent or backend contract.

**When planning:** take the rules below into account when designing the change.

**When reviewing:** report a finding when the diff matches a rule's `Flag:` line, naming the spec, registry, or contract you compared against. Only the cross-SDK reviewer may consult external references.

- Check changes to shared tracer behavior (e.g., context propagation, sampling, configuration, or telemetry) for cross-language impact, using relevant specifications and other tracer implementations as references.
- When an issue stems from the shared design or specification, explicitly flag it as a **design issue** requiring coordination with the affected tracer teams.
- Explain why a .NET-only fix is insufficient or would create divergence.
- Distinguish this from a .NET implementation bug that can be fixed locally to match the agreed behavior.
- Validation across all dd-trace- repositories is not required.

Other repositories:
- [Java (`dd-trace-java`)](https://github.com/DataDog/dd-trace-java)
- [Python (`dd-trace-py`)](https://github.com/DataDog/dd-trace-py)
- [Node.js (`dd-trace-js`)](https://github.com/DataDog/dd-trace-js)
- [Go (`dd-trace-go`)](https://github.com/DataDog/dd-trace-go)
- [PHP (`dd-trace-php`)](https://github.com/DataDog/dd-trace-php)
- [Ruby (`dd-trace-rb`)](https://github.com/DataDog/dd-trace-rb)
- [C++ (`dd-trace-cpp`)](https://github.com/DataDog/dd-trace-cpp)
- [Rust (`libdatadog`)](https://github.com/DataDog/libdatadog)

## 1. Configuration

### XSDK-1.1: Get cross-SDK agreement before adding a product-agnostic global switch
- Flag: a new `DD_*` key with no product prefix (for example, `DD_AGENTLESS_ENABLED`) that changes behaviour across several products (CI Visibility, remote config, discovery, feature flags).

## 2. Telemetry metrics

### XSDK-2.1: Register new telemetry metrics in the backend intake allow list before the tracer emits them
- Flag: new metric entries in `common_metrics.json` or new metric definitions in the PR, with no linked change that adds them to the intake allow list.

## 3. Specs and semantic conventions

### XSDK-3.1: Name the exact OTel reference that span names and tags align with
- Flag: code or comments that claim OTel alignment ("the OTel span name must be ...") without naming the spec version or the instrumentation package and version it matches.
- Not when: the value follows the cited spec or agreed cross-runtime behaviour, even if it looks low-value (for example, `HTTP` as the resource name for unknown methods, or trace flags that are mostly 0 because sampling isn't decided yet).
- ❌ `// The OTel span name must be "{method} {http.route}"`
- ✅ `// Per OTel HTTP semconv v1.x (server spans): "{method} {http.route}"`
