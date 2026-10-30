# Security & Safety

Secrets and sensitive configuration, CI and supply chain, PII, and Dynamic Instrumentation safety and redaction inside customer processes.

**When planning:** take the rules below into account when designing the change.

**When reviewing:** also inspect commit messages and the PR description. Report a finding when the diff matches a rule's `Flag:` line, with the leak path (source to sink).

## 1. Sensitive configuration

### SEC-1.1: Add `sensitive: true` in `supported-configurations.yaml` for every key read with a redacted accessor
- Flag: `AsRedactedString()`/`AsRedactedStringResult()` on a key with no `sensitive: true` entry, or a new `sensitive: true` while sibling keys already read redacted stay unmarked. See [AddingConfigurationKeys.md](../../Configuration/AddingConfigurationKeys.md).

### SEC-1.2: Never write API keys, auth headers or credentialed URLs to logs, span tags, telemetry or committed files
- Flag: a `Log.*`/`SetTag` argument holding an API key, `DD-API-KEY`/`Authorization` header value or URL with user-info, or a committed `.env`/config file containing credentials. See [AGENTS.md](../../../../AGENTS.md#security--configuration).
- ❌ `Log.Debug("Sending request with key {ApiKey}", apiKey);`
- ✅ `Log.Debug("Sending request to {Endpoint}", endpoint);`

### SEC-1.3: Keep customer names, identifying details and customer code out of diffs, commits and PR text
- Flag: a customer/company name, identifying detail, PII or pasted customer code in a test name, comment, sample, commit message or PR description. See [AGENTS.md](../../../../AGENTS.md#security--configuration).

## 2. CI and supply chain

### SEC-2.1: Pin third-party `uses:` to a full 40-char SHA with a matching `# vX.Y.Z` comment, using only allowlisted actions
- Flag: `uses: owner/action@<tag-or-branch>` or an action not on the allowlist; local `./.github/...` refs are exempt. See [GitHubActionsSecurity.md](../../GitHubActionsSecurity.md).
- ❌ `uses: actions/checkout@v4`
- ✅ `uses: actions/checkout@<40-char-sha> # v4.2.2`

### SEC-2.2: Grant a job only the `GITHUB_TOKEN` permissions its steps use when it gets its token from dd-octo-sts
- Flag: job/workflow `permissions:` entries (`contents`, `actions`, `pull-requests`, …) that no step uses, for example permissions left over after switching to a dd-octo-sts token.
- Not when: another step still uses `GITHUB_TOKEN` for that permission (for example, `actions/checkout` needs `contents: read`).
- ❌ `permissions: { id-token: write, actions: read, contents: read } # only the dd-octo-sts step needs a token`
- ✅ `permissions: { id-token: write }`

### SEC-2.3: Pin CI and benchmark images to immutable tags or digests and automate the bumps
- Flag: an `image:`/base-image reference using a rolling tag (`:latest`, `:newest`, `:main`).
- ❌ `image: $REGISTRY/benchmark-runner:newest`
- ✅ `image: $REGISTRY/benchmark-runner:v1.4.2`

## 3. Dynamic Instrumentation

### SEC-3.1: Don't invoke customer getters, `ToString()`, enumerators, attribute constructors or type initializers from debugger code
- Flag: new `PropertyInfo.GetValue` (instance or static), static `FieldInfo.GetValue`, `Expression.Property`/`Expression.PropertyOrField` on a customer type in a generated expression tree, `GetCustomAttributes()`, `Type.GetType`/`Assembly.Load`, `GetEnumerator()`, `Exception.Message`, or arbitrary `ToString()` in debugger capture, expression evaluation, Exception Replay, Code Origin or SymDB paths outside the allowed execution policy. Probe expression member access goes through `TryResolveSafeMemberExpression`, which reads fields and auto-property backing fields instead of calling getters. See [DebuggerSafetyBoundaries.md](../../DebuggerSafetyBoundaries.md).
- ❌ `var text = capturedValue.ToString();`
- ✅ `var text = Redaction.IsSafeToCallToString(capturedValue?.GetType()) ? capturedValue.ToString() : null;`

### SEC-3.2: Decide whether to enumerate, format or redact a value from its runtime type, not its declared type
- Flag: `Redaction.IsSafeToCallToString`, `Redaction.IsSupportedCollection`, an `Exception.Message`/`StackTrace` read, or a redacted-type check decided from the declared type (`FieldInfo.FieldType`, `Expression.Type`) of a value read through a member, index, collection item or interface. A customer subclass or implementation, or a redacted type, can sit behind `object`, an interface or a base class.
- Not when: the declared type is sealed or a value type.
- ❌ `if (Redaction.IsSupportedCollection(field.FieldType)) { /* enumerate */ }`
- ✅ `if (Redaction.IsSupportedCollection(value)) { /* enumerate */ }`

### SEC-3.3: Check dictionary keys for redaction however they are produced, and match keywords against source names
- Flag: a redaction check on a dictionary key that runs only when the key is a literal, not when it is computed from constants or read at runtime; or a redaction keyword matched against a compiler-generated name (`<password>k__BackingField`, a primary-constructor capture, a hoisted local) instead of the source name.
- ❌ `if (indexOrKey is ConstantExpression expr && Redaction.Instance.ShouldRedact(expr.Value?.ToString(), expr.Type, out _)) { return RedactedValue(); }`
- ✅ also check keys computed at runtime: guard the read with a call to `ShouldRedactDictionaryKey(key)` in the generated expression.

### SEC-3.4: Keep anything computed from a redacted value redacted
- Flag: an operator, condition, template segment, capture expression or metric that returns a result computed from a redacted value or from the `{redacted}` placeholder text, such as a comparison result, a string length or a metric number.
- ❌ `eq(secret, "guess")` evaluates to `true` or `false`, so repeated hits leak the value one guess at a time.
- ✅ render `{redacted}`, make the condition false with an evaluation error, and send no metric value.

### SEC-3.5: Keep redacted values and customer data out of evaluation errors and log lines
- Flag: a `Log.*` call or evaluation error about a redaction that includes the redacted value or a dictionary key or type name read at runtime, or an evaluation error that carries the `Message` of an exception customer code or customer data may have produced (a `KeyNotFoundException` message includes its key).
- Not when: the text names only the probe or the expression the user wrote.
- ❌ `result.Errors.Add(new EvaluationError { Expression = expression, Message = e.Message });`
- ✅ `result.Errors.Add(new EvaluationError { Expression = expression, Message = "The expression could not be evaluated." });`

### SEC-3.6: Null-check with `Expression.ReferenceEqual`, not `Expression.Equal`, in generated expression trees
- Flag: `Expression.Equal`/`Expression.NotEqual` against `Expression.Constant(null)` on a value whose type may be a customer type; it binds to a user-defined `operator ==` and runs customer code.
- ❌ `var ifNull = Expression.Equal(expression, Expression.Constant(null));`
- ✅ `var ifNull = Expression.ReferenceEqual(expression, Expression.Constant(null));` (use `HasValue` for `Nullable<T>`)
