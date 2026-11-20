# Response signing conformance fixtures

These fixtures define shared inputs and expected results for the v1 assignment
response signer and client verifiers. Consumers pin an exact repository revision
and run their native implementation against these bytes. The files are test data;
clients do not fetch them when evaluating flags or verifying a response.

## Suites

| File | Coverage | Consumers |
| --- | --- | --- |
| `v1/corpus.json` | 46 response cases: 6 accepted and 40 rejected; 2 private-key matching cases | Verifiers consume `cases`; signers also consume `signing_cases` |
| `v1/isolated-certificate-rules.json` | Accepted control and isolated certificate-size, leaf-CA and intermediate-CA rejections | Signers and verifiers |
| `v1/subject-der.json` | Canonical and nonminimal INTEGER encodings in the leaf subject, with valid signatures | Signers and verifiers |
| `v1/extension-profile.json` | Accepted controls and isolated policy-mapping/empty key-identifier rejections | Signers and verifiers |
| `v1/near-expiry.json` | Four valid near-expiry proofs, a proof outliving its leaf, and exact-expiry rejection | Signers and verifiers |
| `v1/nested-der.json` | Canonical and noncanonical nested INTEGER encodings in certificate extensions | Signers and verifiers |

Version 1 is the first public response-signing protocol. Earlier POC v1/v2 and
the implementation draft called v3 were never public release contracts. Public
v1 requires its complete request/response transcript and leaf/intermediate chain;
it does not accept the older POC format also named v1. X.509 v3 certificate
encoding is independent of this protocol version and remains unchanged.

These vectors regenerate the synthetic chains and signatures for public v1,
preserving the earlier acceptance rules and adding rejection cases for prototype
version headers and the abandoned v3 transcript domain. The [previous extraction](https://github.com/DataDog/ffe-system-test-data/tree/32f2249d66b83b507d68125fd8cda96648ab2bfd/response-signing/v3)
remains available in history. `v1/manifest.json` records the new hashes and
applicability counts; earlier validation receipts do not validate these new bytes.

## Running a consumer

1. Decode `body_base64` into exact bytes. Preserve header order, duplicates and
   URL escaping: several rejection cases exercise those distinctions.
   `response.body_base64` contains application bytes after HTTP content decoding.
   When `wire_body_base64` is present, transport tests serve those compressed bytes
   with the supplied encoding headers; direct verifier tests use `body_base64`.
2. Supply the fixture's `anchor_der_base64` as the local trust anchor and `now`
   as Unix epoch seconds, converting to the native clock unit when needed.
   Use this fixed time even after the synthetic certificates expire.
3. Verify each request/response pair and assert `expected_accept`. For accepted
   cases, compare the constructed transcript with `transcript_base64` as well.
   `rejection_reason` describes the violated rule; it is not a required native
   exception string.
4. Signers additionally load each `signing_cases` credential and its referenced
   response case, then assert `expected_signing`. Clients exclude these cases
   because they do not hold signing keys. The nested-DER supplement's credentials
   let signers exercise the same accepted/rejected certificate boundary.

The positive cases include leaf rotation and a later intermediate under the same
root, without requiring the client to have seen earlier certificates. Negative
cases cover unrelated roots, invalid chain profiles, expiry, changed request or
response bytes, malformed proof headers and signature tampering. Each response
carries its leaf and intermediate; verification requires no certificate fetch.
These fixtures cover buffered response proofs, not a signed NDJSON protocol.

Native adapters, secret-store access assertions, fail-open serving behavior and
implementation-specific diagnostics belong in consumer repositories. Keep these
fixtures independent of those adapters; never derive expected results from the
implementation being tested.

## Synthetic credentials

All certificates, identities, tokens and private keys in this suite are synthetic
test material. The included leaf private keys are intentionally public so signer
implementations can be tested. In `subject-der.json` and `extension-profile.json`,
the root and intermediate reuse that public leaf key. These keys and certificates
must never be trusted or provisioned outside tests. There are no production or
staging credentials or customer responses here.

## Validation and updates

Run `python3 ci/validate-response-signing.py` with `jsonschema` installed. It checks
the schema, exact fixture hashes, case identities, references and applicability
counts. It deliberately allows malformed proof/certificate fields used by
negative cases. Structural validation does not prove cryptographic correctness;
run native signer and verifier tests when changing this contract.

Review expectation changes together with the fixtures and update the manifest
deliberately. Do not reformat signed body bytes, regenerate keys, or normalize
headers during import. Follow the repository's [consumer pinning
rules](../CONTRIBUTING.md#updating-downstream-repositories).

The isolated certificate cases retain valid certificate and response signatures.
The oversized leaf uses a noncritical DER OCTET STRING extension for padding;
the leaf-CA case keeps leaf KeyUsage and no path-length constraint; the
intermediate-CA case keeps CA KeyUsage and path length zero while omitting
the default false CA flag. Each negative changes only the named profile rule.
Consumers must explicitly load this supplement; a manifest entry alone does
not exercise it. Its leaf credentials are public synthetic test material.

## Subject encoding and expiry boundaries

`subject-der.json` holds two otherwise-valid, correctly signed chains and proofs.
The leaf subject contains an attribute with OID `1.2.3.4` and INTEGER value 1.
The canonical encoding is `02 01 01`; `02 02 00 01` must be rejected because
its leading zero is unnecessary. A typed certificate parser can preserve this
opaque attribute during decode/re-encode, so equality of re-encoded bytes alone
is insufficient. Existing extension-payload checks do not cover subject values.
The subject-DER and extension-profile chains reuse the public corpus leaf key
for the root, intermediate and leaf to isolate encoding from signature validity,
so their root and intermediate private keys are public too. These roots and
intermediates keep the corpus certificates' names, serial numbers and key
identifiers with a different key. Use each file's `anchor_der_base64` only for
that file, and never combine supplement anchors in one trust store.
`near-expiry.json` uses the corpus chain unchanged. These are never deployment
credentials.

`near-expiry.json` accepts proofs when the leaf has 301, 300, 299, or 1 second
remaining. Each accepted proof expires at `min(issued_at + 300, leaf.not_after)`.
The negative controls retain valid signatures: one proof extends a single second
past the leaf while staying within the 300-second proof lifetime, and another is
verified exactly when both leaf and proof expire. No live wall clock is used.

`extension-profile.json` requires rejection of policy mappings, even when
noncritical, and nonempty identifiers whenever SKI or AKI is present. Optional
SKI/AKI extensions may be absent. Every rejection retains valid certificate and
proof signatures, with valid and absent-identifier controls.

Load all supplements explicitly. Verifiers consume `cases` and compare exact
transcripts for accepted responses. Signers consume `signing_cases`, resolve
`case_id`, and sign that case's request/body at `now` with the supplied credential.
Assert `expected_signing` separately from `expected_accept`: the overlong proof
must be rejected by a verifier, but its valid credential can still produce a new,
clipped proof. For every successful signing, verify the emitted proof and assert
`now < expires_at <= min(now + 300, leaf.not_after)`. Do not require the signer to
choose the longest possible lifetime or reproduce the fixture's signature bytes.
At exact leaf expiry, signing must fail. Native fallback/transport behavior stays
in consumer tests.

Regenerate only these supplements with:

```sh
python3 -m pip install cryptography==46.0.3
python3 response-signing/generate-boundary-supplements.py
python3 response-signing/generate-boundary-supplements.py --check
python3 ci/validate-response-signing.py
```

The generator uses deterministic ECDSA and the frozen corpus's public synthetic
leaf key and request-transcript prefix. It verifies certificate and response
signatures even for rejection controls; expectations are specified from encoding
and time rules, not derived from an SDK result. CI checks exact regeneration as
well as manifest integrity. If a fixture changes intentionally, update its
manifest hash and review the change. Existing corpus files and the wire protocol
are unchanged. Merge fixture updates before bumping downstream pins or snapshots.
