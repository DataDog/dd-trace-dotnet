import assert from "node:assert/strict"
import { execFileSync, spawn } from "node:child_process"
import { once } from "node:events"
import { createServer } from "node:http"
import { isAbsolute } from "node:path"
import { createInterface } from "node:readline"

const [assembly, library, repeats = "5", filter] = process.argv.slice(2)
assert(assembly && isAbsolute(assembly) && library && isAbsolute(library), "Pass absolute paths to the preview assembly and native library")
assert(Number.isSafeInteger(+repeats) && +repeats > 0, "Pass a positive trial count")

const env = { ...process.env, DOTNET_TieredCompilation: "0" }
const dotnet = process.env.DD_RC_DOTNET ?? "dotnet"
const payloads = JSON.parse(execFileSync(dotnet, [assembly, "--worker-benchmark-fixtures"], { env, encoding: "utf8" }))
const single = payloads.single.map(payload => Buffer.from(payload, "base64"))
const multi = payloads.multi.map(payload => Buffer.from(payload, "base64"))
const unchanged = Buffer.from("{}")
const workloads = [
  { name: "unchanged", files: 0, warm: 700, polls: 9000, variants: single },
  { name: "single", files: 1, warm: 700, polls: 7000, variants: single },
  { name: "multi", files: 24, warm: 300, polls: 1800, variants: multi },
].filter(workload => filter === undefined || workload.name === filter)
assert(workloads.length > 0, "Unknown Remote Config workload")
const orders = [
  ["legacy", "native", "watch"], ["native", "watch", "legacy"], ["watch", "legacy", "native"],
  ["legacy", "watch", "native"], ["watch", "native", "legacy"], ["native", "legacy", "watch"],
]

for (const workload of workloads) {
  for (let trial = 0; trial < +repeats; trial++) {
    for (const backend of orders[trial % orders.length]) {
      const result = await measure(workload, backend)
      console.log(JSON.stringify({ workload: workload.name, trial, backend, polls: workload.polls, ...result }))
    }
  }
}

async function measure ({ files, warm, polls, variants }, backend) {
  let child
  let lines
  let count = 0
  let result
  let failure
  const server = createServer(async (request, response) => {
    try {
      assert.equal(request.url, "/v0.7/config")
      assert.equal(request.method, "POST")
      const index = ++count
      const verify = index === 3 || index === warm + 1 || index === warm + polls + 1
      const chunks = []
      for await (const chunk of request) {
        if (verify) chunks.push(chunk)
      }
      if (verify) {
        const { client } = JSON.parse(Buffer.concat(chunks))
        assert.equal(client.state.config_states.length, files || 1)
        assert(client.state.config_states.every(config => config.apply_state === 2))
        assert.deepEqual(Array.isArray(client.capabilities) ? client.capabilities : [...Buffer.from(client.capabilities, "base64")], [16, 0])
      }

      if (index === 1) await sync("first", "active")
      if (index === warm + 1) await sync("measure", "measuring")
      const last = index === warm + polls + 1
      if (last) await sync("stop", "stopping")
      const body = last || (files === 0 && index > 2) ? unchanged : variants[(index - 1) % variants.length]
      response.writeHead(200, { "Content-Type": "application/json", "Content-Length": body.length })
      response.end(body)
      if (last) {
        const report = await lines.next()
        assert(!report.done, "The tracer exited without a report")
        result = JSON.parse(report.value)
      }
    } catch (error) {
      failure ??= error
      response.destroy(error)
      child?.kill()
    }
  })

  try {
    server.listen(0, "127.0.0.1")
    await once(server, "listening")
    const url = `http://127.0.0.1:${server.address().port}`
    child = spawn(dotnet, [assembly, "--measure-live-worker", url, backend, library], {
      env,
      signal: AbortSignal.timeout(30_000),
    })
    let stderr = ""
    child.stderr.setEncoding("utf8")
    child.stderr.on("data", chunk => { stderr = (stderr + chunk).slice(-4000) })
    const output = createInterface({ input: child.stdout })
    lines = output[Symbol.asyncIterator]()
    const closed = once(child, "close")
    const [code] = await closed
    if (failure) throw failure
    assert.equal(code, 0, stderr)
    assert.equal(count, warm + polls + 1)
    assert(result, "The tracer exited before the requested polls finished")
    assert.equal(result.delivered, polls * files)
    return result
  } finally {
    if (child?.exitCode === null) child.kill()
    server.closeAllConnections()
    server.close()
  }

  async function sync (signal, expected) {
    child.stdin.write(`${signal}\n`)
    const line = await lines.next()
    assert.equal(line.value, expected)
  }
}
