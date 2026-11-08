# Rules fixture artifacts

Install the locked serializer with `npm ci --prefix tools/ufc --ignore-scripts`.

```sh
npm --prefix tools/ufc run generate -- --converter /absolute/path/to/ufc-fixtures --converter-sha256 EXPECTED_SHA256 --converter-source /absolute/path/to/source-root --converter-source-files /outside/repository/source-files.json
npm --prefix tools/ufc run check -- --converter /absolute/path/to/ufc-fixtures --converter-sha256 EXPECTED_SHA256 --converter-source /absolute/path/to/source-root --converter-source-files /outside/repository/source-files.json
node tools/ufc/validate-rules-resources.mjs
node --test tools/ufc/rules-resources.test.mjs
python3 tools/ufc/validate-rules-schema.py
```

The catalog sends the complete original `ufc-config.json` directly to the production
Go converter. There is no authored flag selection or normalization. The converter
attempts every flag and reports included or omitted with an explicit error reason.
Generated JSON retains original metadata and included flag definitions; only
reported failed flags are removed. Canonical JSON and evaluation cases never change.

Each resource commits raw `ufc.pb` and `configuration-wire.json`. Generation also
writes local `ufc.generated.json`, `report.json` and `parity.json` diagnostics. The wire file is the UTF-8 portable
wire string itself, without extra JSON quoting. Public pinned
`@datadog/flagging-core@3.0.1` APIs decode protobuf, serialize wire, and parse it
again. Parser failure, empty configurations, changed semantic payload and unstable
wire serialization fail generation. No timestamps, ETags or split reasons are added.
Reasons survive conversion when present in `ufc-config.json`. The canonical splits
have no `reason`, so its absence becomes `REASON_UNSPECIFIED (0)`, which the
`@datadog/flagging-core` 3.0.1 protobuf evaluator maps to `UNKNOWN` for every
evaluation that selects a split. Consumers cannot assert the canonical reason for
those results against these artifacts. The diagnostic retains these differences.


`parity.json` is a gitignored local diagnostic: existing expectations are compared with original JSON,
filtered JSON, binary and wire for converted flags and deliberately nonexistent keys.
Only cases for source flags omitted during conversion are omitted from evaluation.
The comparison covers value, reason, errorCode, serialId and doLog, which the pinned
evaluator exposes. Split features are not returned by that API and are not compared.
The report is therefore limited to those observable fields, not full corpus parity.
SDK mismatches never filter flags, rewrite expectations, or block artifact generation.
A successful generation is serialization evidence, not a green evaluation suite or
browser offline initialization proof. The evaluation clock is fixed in the diagnostic.

Generation stages the whole artifact set before replacing it. Check mode writes
nothing in the repository and detects stale, missing and unexpected artifacts.
The manifest hashes each source UFC, the catalogue, the converter lock, generator,
package manifests and committed protobuf/wire artifacts. Evaluation cases, schemas,
documentation, tests and local diagnostics are excluded from artifact provenance.
The source lock records one opaque SHA256 digest of the converter source inventory and contents.
The local inventory is a JSON array of relative source file paths supplied by the
converter maintainer; keep it outside this repository. Include all relevant codec
and CLI sources and refresh the inventory when source membership changes. Neither
the inventory, repository location nor source revision is published. The supplied executable
must match its expected SHA256. Source correspondence is declared build provenance,
not a cryptographic build attestation. No immutable published converter distribution
is configured; local generation uses a separately supplied converter CLI. No CI added.

`ufc.generated.json`, `report.json` and `parity.json` are local diagnostics: gitignored and excluded from the committed artifact manifest. Regenerate them locally when inspecting conversion. The report includes a structural JSON comparison (ignoring whitespace/object-key order): exact source equality, equality after only reported omissions, and the omitted flag keys. With omitted flags, exact equality is false even when all retained content is unchanged.

Validation works without local diagnostics. When present, they are checked against
the source and current artifacts; missing diagnostics do not invalidate a clean
checkout. Repeat-generation checks compare only committed artifacts.
