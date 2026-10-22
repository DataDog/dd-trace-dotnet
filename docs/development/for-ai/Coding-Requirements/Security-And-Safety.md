# Security & Safety

Secrets and sensitive configuration, CI and supply chain, PII, and safety inside customer processes.

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

## 3. Customer-process safety

### SEC-3.1: Don't invoke customer getters, `ToString()`, enumerators, attribute constructors or type initializers from debugger code
- Flag: new `PropertyInfo.GetValue`, static `FieldInfo.GetValue`, `GetCustomAttributes()`, `Type.GetType`/`Assembly.Load`, `GetEnumerator()`, `Exception.Message`, or arbitrary `ToString()` in debugger capture, expression evaluation, Exception Replay, Code Origin or SymDB paths outside the allowed execution policy. See [DebuggerSafetyBoundaries.md](../../DebuggerSafetyBoundaries.md).
- ❌ `var text = capturedValue.ToString();`
- ✅ `var text = Redaction.IsSafeToCallToString(type) ? capturedValue.ToString() : null;`
