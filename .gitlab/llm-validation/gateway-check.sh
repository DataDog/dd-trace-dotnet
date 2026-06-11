#!/usr/bin/env bash
# One-off entitlement check (run before building the full .NET+node image):
# can this CI identity mint a rapid-ai-platform token and reach the AI gateway?
# Minimal deps — curl + jq only. See README.md.
set -uo pipefail

GW="${ANTHROPIC_BASE_URL:-https://ai-gateway.us1.ddbuild.io}"

echo "=== downloading authanywhere ==="
curl -fsSL -o /usr/local/bin/authanywhere \
  "https://binaries.ddbuild.io/dd-source/authanywhere/LATEST/authanywhere-linux-amd64"
chmod +x /usr/local/bin/authanywhere

echo "=== minting rapid-ai-platform token ==="
if ! AI_BEARER="$(authanywhere --audience rapid-ai-platform)"; then
  echo "FAIL: authanywhere could not mint a rapid-ai-platform token (entitlement missing for this CI identity?)."
  exit 1
fi
echo "token minted (length ${#AI_BEARER})"   # never print the token itself

echo "=== pinging $GW/v1/messages ==="
HTTP="$(curl -sS -o /tmp/gw-resp.json -w '%{http_code}' \
  -X POST "$GW/v1/messages" \
  -H "anthropic-version: 2023-06-01" \
  -H "x-api-key: not-set" \
  -H "source: claude-code" -H "org-id: 2" -H "provider: anthropic" -H "claude-code: true" \
  -H "$AI_BEARER" \
  -H "content-type: application/json" \
  -d '{"model":"claude-opus-4-8","max_tokens":16,"messages":[{"role":"user","content":"reply with the single word ok"}]}')"

echo "HTTP $HTTP"
echo "--- response (truncated) ---"
head -c 600 /tmp/gw-resp.json 2>/dev/null; echo

if [ "$HTTP" = "200" ]; then
  echo "RESULT: OK — rapid-ai-platform entitlement confirmed and the gateway answered."
  echo "        (c) can run once a CI image with .NET 8 SDK + node/npm is in place."
  exit 0
else
  echo "RESULT: NOT OK (HTTP $HTTP) — likely the rapid-ai-platform entitlement is missing for this CI identity"
  echo "        (401 = token/auth problem; 403 = not entitled)."
  echo "        Action: request the rapid-ai-platform grant for dd-trace-dotnet CI from the AI-gateway / dd-source owners."
  exit 1
fi
