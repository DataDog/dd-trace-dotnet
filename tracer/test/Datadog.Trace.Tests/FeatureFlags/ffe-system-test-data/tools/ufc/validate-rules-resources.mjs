// Local artifact integrity and evaluation parity checks.
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
import { promises as fs } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { root, sha256, packageWire, sourcePath, generatorInputFiles } from './generate.mjs';
import { parity } from './parity.mjs';
const { configurationFromRulesBinary } = createRequire(import.meta.url)('@datadog/flagging-core/rules-based');
export async function validate(base = root) {
  const generated = path.join(base, 'rules-resources/generated');
  const manifest = JSON.parse(await fs.readFile(path.join(generated, 'manifest.json')));
  const catalog = JSON.parse(await fs.readFile(path.join(base, 'rules-resources/catalog.json')));
  assert.equal(manifest.catalogSha256, sha256(await fs.readFile(path.join(base, 'rules-resources/catalog.json'))), 'Catalog changed: regenerate rules resources');
  for (const [relative, expected] of Object.entries(manifest.generatorInputs)) {
    assert.equal(sha256(await fs.readFile(await sourcePath(relative, base))), expected, `${relative} changed: regenerate rules resources`);
  }
  const expectedInputs = [...new Set([...catalog.resources.map(x => x.source), ...generatorInputFiles])];
  assert.deepEqual(Object.keys(manifest.generatorInputs).sort(), expectedInputs.sort(), 'Generator input membership changed');
  assert.deepEqual(Object.keys(manifest.converter), ['source'], 'Converter metadata must contain only opaque provenance');
  assert.deepEqual(Object.keys(manifest.converter.source), ['sha256'], 'Source provenance must not publish identifiers');
  assert.match(manifest.converter.source.sha256, /^[a-f0-9]{64}$/);
  assert.deepEqual(manifest.converter.source, JSON.parse(await fs.readFile(path.join(base, 'tools/ufc/converter-lock.json'))));
  const expectedFiles = ['manifest.json'];
  let exercised = 0;
  assert.deepEqual(manifest.resources.map(x => x.id), catalog.resources.map(x => x.id));
  for (const resource of manifest.resources) {
    assert.match(resource.id, /^[a-z0-9]+(?:-[a-z0-9]+)*$/);
    assert.equal(resource.sourceSha256, sha256(await fs.readFile(await sourcePath(resource.source, base))), `${resource.source} changed: regenerate rules resources`);
    const definition = catalog.resources.find(x => x.id === resource.id);
    assert.equal(resource.source, definition.source, 'Resource source differs from catalogue');

    for (const required of ['ufc.pb', 'configuration-wire.json']) assert.ok(Object.hasOwn(resource.artifacts, required), `Missing artifact contract: ${required}`);
    for (const name of ['ufc.generated.json', 'report.json', 'parity.json']) assert.ok(!Object.hasOwn(resource.artifacts, name), 'Local diagnostics must not be committed manifest artifacts');
    for (const [name, entry] of Object.entries(resource.artifacts)) {
      assert.equal(path.basename(name), name, 'Artifact names must be local filenames');
      const relative = `${resource.id}/${name}`;
      expectedFiles.push(relative);
      assert.equal(sha256(await fs.readFile(await sourcePath(`rules-resources/generated/${relative}`, base))), entry.sha256, `${relative} changed: regenerate rules resources`);
    }
    const directory = path.join(generated, resource.id);
    const binary = await fs.readFile(path.join(directory, 'ufc.pb'));
    const wire = await fs.readFile(path.join(directory, 'configuration-wire.json'), 'utf8');
    assert.equal(packageWire(binary), wire, 'Wire differs from canonical SDK serialization');
    const source = JSON.parse(await fs.readFile(await sourcePath(resource.source, base)));
    const convertedFlagKeys = Object.keys(configurationFromRulesBinary(binary).rules.response.flags).sort();
    const included = new Set(convertedFlagKeys);
    const omittedFlagKeys = Object.keys(source.flags).filter(key => !included.has(key)).sort();
    // Consumers use these inventories to skip omissions, so derive them from the payload.
    assert.deepEqual(resource.convertedFlagKeys, convertedFlagKeys, 'Converted flag inventory differs from binary');
    assert.deepEqual(resource.omittedFlagKeys, omittedFlagKeys, 'Omitted flag inventory differs from source minus binary');
    const filtered = structuredClone(source);
    for (const key of Object.keys(filtered.flags)) if (!included.has(key)) delete filtered.flags[key];
    for (const key of included) assert.ok(Object.hasOwn(source.flags, key), 'Binary introduced a flag absent from source');
    async function localDiagnostic(name) {
      try {
        const bytes = await fs.readFile(path.join(directory, name));
        const diagnostic = JSON.parse(bytes);
        assert.ok(diagnostic !== null && typeof diagnostic === 'object' && !Array.isArray(diagnostic), `${name} must contain a JSON object`);
        expectedFiles.push(`${resource.id}/${name}`);
        return diagnostic;
      }
      catch (error) { if (error.code === 'ENOENT') return null; throw error; }
    }
    const localJson = await localDiagnostic('ufc.generated.json');
    if (localJson) assert.deepEqual(localJson, filtered, 'Local JSON may only remove protobuf-omitted flags');
    const conversion = await localDiagnostic('report.json');
    if (conversion) {
      assert.equal(conversion.sourceSha256, resource.sourceSha256);
      assert.deepEqual(conversion.flags.map(x => x.key).sort(), Object.keys(source.flags).sort(), 'Every source flag must be reported');
      const omitted = conversion.flags.filter(flag => flag.status === 'omitted').map(flag => flag.key).sort();
      assert.deepEqual(conversion.jsonComparison, { matchesOriginal: omitted.length === 0, matchesOriginalExceptOmittedFlags: true, omittedFlags: omitted }, 'Go JSON comparison did not verify preservation');
      for (const flag of conversion.flags) {
        assert.equal(flag.status, included.has(flag.key) ? 'included' : 'omitted');
        if (flag.status === 'omitted') assert.ok(flag.error, 'Omission requires warning reason');
      }
    }
    const report = parity(base, binary, wire, filtered, source);
    const localParity = await localDiagnostic('parity.json');
    if (localParity) assert.deepEqual(report, localParity, 'Local evaluation diagnostics changed');
    exercised += report.exercisedCases;
  }
  async function walk(directory, prefix = '') {
    const result = [];
    for (const entry of await fs.readdir(directory, { withFileTypes: true })) {
      assert.ok(!entry.isSymbolicLink(), 'Generated symlinks are forbidden');
      const relative = prefix + entry.name;
      if (entry.isDirectory()) result.push(...await walk(path.join(directory, entry.name), relative + '/'));
      else result.push(relative);
    }
    return result.sort();
  }
  assert.deepEqual(await walk(generated), expectedFiles.sort(), 'Missing or extra generated artifacts');
  return { resources: manifest.resources.length, exercised };
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { console.log('Rules integrity/parity:', await validate()); }
  catch (error) { console.error(error.message); process.exitCode = 1; }
}
