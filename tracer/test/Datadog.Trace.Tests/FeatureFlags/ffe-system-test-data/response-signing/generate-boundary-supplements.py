#!/usr/bin/env python3
"""Reproduce synthetic boundary fixtures with cryptography==46.0.3.

Reuse the frozen corpus's request prefix and public test key, never an SDK signer.
Expectations follow DER minimality and the v1 certificate/proof time bounds.
"""
import argparse
import base64
import copy
import hashlib
import json
from pathlib import Path
import struct

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec

DIRECTORY = Path(__file__).resolve().parent / "v1"
PREFIX = "x-datadog-feature-flags-"
SOURCE_SHA256 = "393490bfefe382b9b75cf2964c131a910faa679888689457f99c2da2dc57fbbd"


def decode(value):
    return base64.b64decode(value, validate=True)


def encode(value):
    return base64.b64encode(value).decode("ascii")


def digest(value):
    return hashlib.sha256(value).digest()


def tlv(tag, content):
    size = len(content)
    length = bytes([size]) if size < 128 else bytes([0x82]) + size.to_bytes(2, "big")
    if 128 <= size < 256:
        length = bytes([0x81, size])
    return bytes([tag]) + length + content


def children(data):
    """Split one known constructed DER fixture value, preserving raw children."""
    def bounds(offset):
        length = data[offset + 1]
        start = offset + 2
        if length & 128:
            count = length & 127
            length = int.from_bytes(data[start:start + count], "big")
            start += count
        return start, start + length
    start, end = bounds(0)
    assert end == len(data)
    result = []
    while start < end:
        _, next_offset = bounds(start)
        assert start < next_offset <= end
        result.append(data[start:next_offset])
        start = next_offset
    return result


def signature(key, data):
    return key.sign(data, ec.ECDSA(hashes.SHA256(), deterministic_signing=True))


def resign(certificate, key, replacements):
    fields = children(certificate)
    tbs = children(fields[0])
    for index, value in replacements.items():
        tbs[index] = value
    tbs = tlv(0x30, b"".join(tbs))
    return tlv(0x30, tbs + fields[1] + tlv(3, b"\0" + signature(key, tbs)))


def response_case(base, key, credential, case_id, now, issued, expires, accepted, reason):
    case = copy.deepcopy(base)
    case.pop("credential", None)
    case.update(id=case_id, now=now, expected_accept=accepted, rejection_reason=reason)
    leaf = decode(credential["certificate_der_base64"])
    intermediate = decode(credential["intermediate_der_base64"])
    body = decode(case["response"]["body_base64"])
    # Final 122 bytes: status, issued, expiry, body length, and three hashes.
    prefix = decode(base["transcript_base64"])[:-122]
    transcript = prefix + struct.pack(">HQQQ", 200, issued, expires, len(body))
    transcript += digest(body) + digest(leaf) + digest(intermediate)
    updates = {
        "signing-certificate": encode(leaf), "signing-intermediate": encode(intermediate),
        "certificate-id": digest(leaf).hex(), "issued-at": str(issued),
        "expires-at": str(expires), "signature": encode(signature(key, transcript)),
    }
    case["response"]["headers"] = [
        [name, updates.get(name.removeprefix(PREFIX), value)]
        for name, value in case["response"]["headers"]
    ]
    case["transcript_base64"] = encode(transcript)
    return case


def verify_signatures(corpus):
    """Rejection controls still have valid signatures over the exact raw bytes."""
    root = decode(corpus["anchor_der_base64"])
    root_key = serialization.load_der_public_key(children(children(root)[0])[6])
    for case in corpus["cases"]:
        headers = dict(case["response"]["headers"])
        intermediate = decode(headers[PREFIX + "signing-intermediate"])
        leaf = decode(headers[PREFIX + "signing-certificate"])
        parent = root_key
        for certificate in [intermediate, leaf]:
            fields = children(certificate)
            # Short BIT STRING containing a signature; avoid parsing the malformed subject.
            assert fields[2][0] == 3 and fields[2][1] < 128 and fields[2][2] == 0
            parent.verify(fields[2][3:], fields[0], ec.ECDSA(hashes.SHA256()))
            parent = serialization.load_der_public_key(children(fields[0])[6])
        parent.verify(decode(headers[PREFIX + "signature"]),
                      decode(case["transcript_base64"]), ec.ECDSA(hashes.SHA256()))


def generate():
    raw = (DIRECTORY / "corpus.json").read_bytes()
    assert hashlib.sha256(raw).hexdigest() == SOURCE_SHA256
    source = json.loads(raw)
    base = source["cases"][0]
    credential = base["credential"]
    key = serialization.load_der_private_key(decode(credential["private_key_pkcs8_der_base64"]), None)
    original_leaf = decode(credential["certificate_der_base64"])
    spki = children(children(original_leaf)[0])[6]
    root = resign(decode(source["anchor_der_base64"]), key, {6: spki})
    intermediate = resign(decode(credential["intermediate_der_base64"]), key, {6: spki})
    subject = {"schema_version": 1, "synthetic": True, "anchor_der_base64": encode(root),
               "cases": [], "signing_cases": []}
    for case_id, integer, accepted in [("canonical-subject-integer", b"\x01", True),
                                       ("nonminimal-subject-integer", b"\x00\x01", False)]:
        # Unknown attribute OID 1.2.3.4 with an ASN.1 ANY INTEGER value.
        name = tlv(0x30, tlv(0x31, tlv(0x30, b"\x06\x03\x2a\x03\x04" + tlv(2, integer))))
        cred = dict(credential, certificate_der_base64=encode(resign(original_leaf, key, {5: name})),
                    intermediate_der_base64=encode(intermediate))
        now = base["now"]
        reason = None if accepted else "Nonminimal INTEGER in leaf subject; cryptographic signatures remain valid"
        subject["cases"].append(response_case(base, key, cred, case_id, now, now, now + 300, accepted, reason))
        subject["signing_cases"].append(dict(id=case_id, case_id=case_id, credential=cred, expected_signing=accepted))
    end = int(x509.load_der_x509_certificate(original_leaf).not_valid_after_utc.timestamp())
    expiry = {"schema_version": 1, "synthetic": True, "anchor_der_base64": source["anchor_der_base64"],
              "cases": [], "signing_cases": []}
    for remaining in [301, 300, 299, 1]:
        case_id = f"leaf-expires-in-{remaining}-seconds"
        now = end - remaining
        expiry["cases"].append(response_case(base, key, credential, case_id, now, now, min(now + 300, end), True, None))
    expiry["cases"].append(response_case(base, key, credential, "proof-outlives-leaf-by-one-second",
                                         end - 299, end - 299, end + 1, False,
                                         "Proof exceeds leaf validity despite a valid signature and 300-second lifetime"))
    expiry["cases"].append(response_case(base, key, credential, "at-leaf-and-proof-expiry",
                                         end, end - 1, end, False, "Proof and leaf are expired at the verification clock"))
    for case in expiry["cases"]:
        expiry["signing_cases"].append(dict(id=case["id"], case_id=case["id"], credential=credential,
                                             expected_signing=case["now"] < end))
    extensions = {"schema_version": 1, "synthetic": True, "anchor_der_base64": encode(root),
                  "cases": [], "signing_cases": []}
    original_extensions = children(children(children(original_leaf)[0])[7])[0]
    for case_id, oid, value, accepted in [
        ("extension-profile-valid", None, None, True),
        ("noncritical-policy-mappings", b"\x06\x03\x55\x1d\x21",
         b"\x30\x0c\x30\x0a\x06\x03\x2a\x03\x04\x06\x03\x2a\x03\x05", False),
        ("empty-authority-key-identifier", b"\x06\x03\x55\x1d\x23", b"\x30\x00", False),
        ("empty-authority-key-value", b"\x06\x03\x55\x1d\x23", b"\x30\x02\x80\x00", False),
        ("empty-subject-key-identifier", b"\x06\x03\x55\x1d\x0e", b"\x04\x00", False),
        ("absent-optional-key-identifiers", None, None, True),
    ]:
        entries = children(original_extensions)
        if oid:
            replacement = tlv(0x30, oid + tlv(4, value))
            if any(children(entry)[0] == oid for entry in entries):
                entries = [replacement if children(entry)[0] == oid else entry for entry in entries]
            else:
                entries.append(replacement)
        elif case_id == "absent-optional-key-identifiers":
            entries = [entry for entry in entries
                       if children(entry)[0] not in [b"\x06\x03\x55\x1d\x0e", b"\x06\x03\x55\x1d\x23"]]
        leaf = resign(original_leaf, key, {7: tlv(0xa3, tlv(0x30, b"".join(entries)))})
        cred = dict(credential, certificate_der_base64=encode(leaf), intermediate_der_base64=encode(intermediate))
        now = base["now"]
        reason = None if accepted else "Unsupported certificate extension form despite valid signatures"
        extensions["cases"].append(response_case(base, key, cred, case_id, now, now, now + 300, accepted, reason))
        extensions["signing_cases"].append(dict(id=case_id, case_id=case_id, credential=cred, expected_signing=accepted))
    results = {"subject-der.json": subject, "near-expiry.json": expiry, "extension-profile.json": extensions}
    for corpus in results.values():
        verify_signatures(corpus)
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Check reproducibility without writing")
    args = parser.parse_args()
    for name, corpus in generate().items():
        raw = (json.dumps(corpus, indent=2) + "\n").encode()
        path = DIRECTORY / name
        if args.check:
            assert path.read_bytes() == raw, f"{name}: regeneration differs"
        else:
            path.write_bytes(raw)
        print(f"{name}: {len(corpus['cases'])} cases, certificate and proof signatures verified")


if __name__ == "__main__":
    main()
