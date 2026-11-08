import { test } from 'node:test';
import assert from 'node:assert/strict';
import { promises as fs } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { root, compareTrees, packageWire, sourcePath, converterIdentity } from './generate.mjs';
import { validate } from './validate-rules-resources.mjs';
import { parity } from './parity.mjs';
async function temporary(fn) {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'rules-validation-'));
  try { await fn(directory); } finally { await fs.rm(directory, { recursive: true, force: true }); }
}
test('whole-tree comparison catches missing, stale and untracked extra files', async () => temporary(async directory => {
  const expected = path.join(directory, 'expected'), actual = path.join(directory, 'actual');
  await fs.mkdir(expected); await fs.mkdir(actual);
  await fs.writeFile(path.join(expected, 'a'), 'new');
  assert.deepEqual(await compareTrees(expected, actual), ['missing: a']);
  await fs.writeFile(path.join(actual, 'a'), 'old');
  await fs.writeFile(path.join(actual, 'extra'), 'extra');
  assert.deepEqual(await compareTrees(expected, actual), ['stale: a', 'unexpected: extra']);
  await fs.writeFile(path.join(expected, 'local.json'), '{}');
  assert.deepEqual(await compareTrees(expected, actual, ['local.json']), ['stale: a', 'unexpected: extra']);
}));
test('wire packaging rejects empty and malformed protobuf', () => {
  assert.throws(() => packageWire(Buffer.alloc(0)));
  assert.throws(() => packageWire(Buffer.from([255])));
  assert.throws(() => packageWire(Buffer.from([8, 1])));
});
test('integrity gate rejects source drift, deletion, extra output and changed tool pin', async () => temporary(async directory => {
  // Keep fixtures independent of repository state and avoid copying node_modules.
  for (const name of ['ufc-config.json', 'evaluation-cases', 'rules-resources', 'schemas']) await fs.cp(path.join(root, name), path.join(directory, name), { recursive: true });
  await fs.cp(path.join(root, 'tools/ufc'), path.join(directory, 'tools/ufc'), { recursive: true, filter: file => !file.split(path.sep).includes('node_modules') });
  await validate(directory);
  const generated = path.join(directory, 'rules-resources/generated/canonical-evaluator');
  const manifest = JSON.parse(await fs.readFile(path.join(directory, 'rules-resources/generated/manifest.json')));
  const parityPath = path.join(generated, 'parity.json');
  await fs.writeFile(parityPath, '{}');
  await assert.rejects(validate(directory), /Local evaluation diagnostics changed/);
  for (const name of ['ufc.generated.json', 'report.json', 'parity.json']) {
    assert.ok(!Object.hasOwn(manifest.resources[0].artifacts, name));
    await fs.rm(path.join(generated, name), { force: true });
  }
  await validate(directory); // A fresh checkout has no local conversion diagnostics.
  // Changes unrelated to committed bytes must not require converter regeneration.
  for (const file of ['tools/ufc/README.md', 'tools/ufc/parity.mjs', 'schemas/precomputed-assignment.schema.json']) await fs.appendFile(path.join(directory, file), '\n');
  await fs.writeFile(path.join(directory, 'tools/ufc/.DS_Store'), 'unrelated');
  await fs.writeFile(path.join(directory, 'evaluation-cases/additional.json'), '[]');
  await validate(directory);
  const generator = path.join(directory, 'tools/ufc/generate.mjs');
  const generatorBytes = await fs.readFile(generator);
  await fs.appendFile(generator, '\n');
  await assert.rejects(validate(directory), /generate.mjs changed/);
  await fs.writeFile(generator, generatorBytes);

  for (const name of ['ufc.generated.json', 'report.json', 'parity.json']) {
    const diagnosticPath = path.join(generated, name);
    for (const value of [null, false, 0, '', [], true, 1, 'invalid']) {
      await fs.writeFile(diagnosticPath, JSON.stringify(value));
      await assert.rejects(validate(directory), /must contain a JSON object/, `${name}: ${JSON.stringify(value)}`);
    }
    await fs.rm(diagnosticPath);
  }
  await validate(directory); // Absent diagnostics still validate after rejecting malformed roots.
  // Inventories control consumer skips and must match the payload even without diagnostics.
  const manifestPath = path.join(directory, 'rules-resources/generated/manifest.json');
  for (const field of ['convertedFlagKeys', 'omittedFlagKeys']) {
    for (const mutation of ['missing', 'duplicate', 'unsorted']) {
      const changed = structuredClone(manifest);
      if (mutation === 'missing') delete changed.resources[0][field];
      if (mutation === 'duplicate') changed.resources[0][field].push(changed.resources[0][field][0]);
      if (mutation === 'unsorted') changed.resources[0][field].reverse();
      await fs.writeFile(manifestPath, JSON.stringify(changed));
      await assert.rejects(validate(directory), /flag inventory differs/, `${field}: ${mutation}`);
    }
  }
  // An alternate identical UFC must not override the catalogue's source contract.
  await fs.copyFile(path.join(directory, 'ufc-config.json'), path.join(directory, 'alternate-ufc.json'));
  const redirected = structuredClone(manifest);
  redirected.resources[0].source = 'alternate-ufc.json';
  await fs.writeFile(manifestPath, JSON.stringify(redirected));
  await assert.rejects(validate(directory), /Resource source differs from catalogue/);
  await fs.rm(path.join(directory, 'alternate-ufc.json'));
  const swapped = structuredClone(manifest);
  const resource = swapped.resources[0];
  [resource.convertedFlagKeys[0], resource.omittedFlagKeys[0]] = [resource.omittedFlagKeys[0], resource.convertedFlagKeys[0]];
  resource.convertedFlagKeys.sort(); resource.omittedFlagKeys.sort();
  await fs.writeFile(manifestPath, JSON.stringify(swapped));
  await assert.rejects(validate(directory), /Converted flag inventory differs from binary/);
  await fs.writeFile(manifestPath, JSON.stringify(manifest));
  await validate(directory);
  const source = path.join(directory, 'ufc-config.json'), original = await fs.readFile(source);
  await fs.appendFile(source, '\n'); await assert.rejects(validate(directory)); await fs.writeFile(source, original);
  const artifact = path.join(directory, 'rules-resources/generated/canonical-evaluator/ufc.pb'), bytes = await fs.readFile(artifact);
  await fs.unlink(artifact); await assert.rejects(validate(directory)); await fs.writeFile(artifact, bytes);
  const extra = path.join(directory, 'rules-resources/generated/untracked');
  await fs.writeFile(extra, 'extra'); await assert.rejects(validate(directory)); await fs.unlink(extra);
  const pin = path.join(directory, 'tools/ufc/package.json');
  await fs.appendFile(pin, '\n'); await assert.rejects(validate(directory));
}));

test('source paths cannot leave repository', async () => temporary(async directory => {
  const repository = path.join(directory, 'repository');
  await fs.mkdir(repository);
  const inside = path.join(repository, 'inside.json');
  const outside = path.join(directory, 'outside.json');
  await fs.writeFile(inside, '{}');
  await fs.writeFile(outside, '{}');
  await fs.symlink(outside, path.join(repository, 'link.json'));
  await assert.rejects(sourcePath('../outside.json', repository), /Unsafe source path/);
  await assert.rejects(sourcePath(inside, repository), /Unsafe source path/);
  await assert.rejects(sourcePath('link.json', repository), /Source escapes repository/);
  assert.equal(await sourcePath('inside.json', repository), await fs.realpath(inside));
}));

test('parity evaluates the explicit source without requiring a root UFC file', async () => temporary(async directory => {
  await fs.cp(path.join(root, 'evaluation-cases'), path.join(directory, 'evaluation-cases'), { recursive: true });
  const source = JSON.parse(await fs.readFile(path.join(root, 'ufc-config.json')));
  const generated = path.join(root, 'rules-resources/generated/canonical-evaluator');
  const binary = await fs.readFile(path.join(generated, 'ufc.pb'));
  const wire = await fs.readFile(path.join(generated, 'configuration-wire.json'), 'utf8');
  const baseline = parity(directory, binary, wire, source, source);
  const differentSource = { ...source, flags: {} };
  const changed = parity(directory, binary, wire, source, differentSource);
  assert.ok(baseline.exercisedCases > 0);
  const originalResults = report => report.results.map(result => result.mismatches.filter(mismatch => mismatch.encoding === 'original'));
  const otherResults = report => report.results.map(result => result.mismatches.filter(mismatch => mismatch.encoding !== 'original'));
  assert.notDeepEqual(originalResults(changed), originalResults(baseline));
  assert.deepEqual(otherResults(changed), otherResults(baseline));
}));

test('wire packaging preserves unknown protobuf fields across byte containers', async () => {
  const binary = await fs.readFile(path.join(root, 'rules-resources/generated/canonical-evaluator/ufc.pb'));
  // Append unknown field 99 with varint value 1; it must survive the public wire API.
  const extended = Buffer.concat([binary, Buffer.from([0x98, 0x06, 0x01])]);
  const wire = packageWire(extended);
  const embedded = Buffer.from(JSON.parse(wire).rules.response, 'base64');
  assert.ok(embedded.includes(Buffer.from([0x98, 0x06, 0x01])));
  assert.equal(packageWire(new Uint8Array(extended)), wire);
});


test('parity reads SDK metadata and freezes constructor time without changing explicit dates', async () => temporary(async directory => {
  const source = JSON.parse(await fs.readFile(path.join(root, 'ufc-config.json')));
  const generated = path.join(root, 'rules-resources/generated/canonical-evaluator');
  const binary = await fs.readFile(path.join(generated, 'ufc.pb'));
  const wire = await fs.readFile(path.join(generated, 'configuration-wire.json'), 'utf8');
  const cases = JSON.parse(await fs.readFile(path.join(root, 'evaluation-cases/test-case-split-features.json'))).filter((_, index) => index !== 1);
  await fs.mkdir(path.join(directory, 'evaluation-cases'));
  await fs.writeFile(path.join(directory, 'evaluation-cases/metadata.json'), JSON.stringify(cases));
  const serialCases = JSON.parse(await fs.readFile(path.join(root, 'evaluation-cases/test-case-serial-id-metadata.json')));
  await fs.writeFile(path.join(directory, 'evaluation-cases/serial.json'), JSON.stringify(serialCases));
  assert.ok(serialCases.some(row => row.result.serialId === null));
  const RealDate = globalThis.Date;
  const fixedTime = RealDate.parse('2026-01-01T00:00:00.000Z');
  let observed = 0;
  // Observe the actual SDK evaluation boundary while the parity clock is installed.
  Object.defineProperty(source, 'flags', { get() {
    if (globalThis.Date === RealDate) return JSON.parse(readSource).flags;
    observed++;
    assert.equal(Date.now(), fixedTime);
    assert.equal(new Date().getTime(), fixedTime);
    assert.equal(new Date(0).getTime(), 0);
    assert.equal(new Date('2025-01-01').getUTCFullYear(), 2025);
    return JSON.parse(readSource).flags;
  }});
  const readSource = await fs.readFile(path.join(root, 'ufc-config.json'), 'utf8');
  const report = parity(directory, binary, wire, source, source);
  assert.ok(observed > 0);
  assert.equal(globalThis.Date, RealDate);
  const mismatches = report.results.flatMap(row => row.mismatches);
  assert.deepEqual(mismatches.filter(row => Object.hasOwn(row, 'error')), []);
  const metadataMismatches = mismatches.filter(row => ['serialId', 'doLog'].includes(row.field));
  assert.deepEqual(metadataMismatches, []);
  assert.ok(cases.some(row => row.result.serialId === 0));
  assert.ok(cases.some(row => row.result.doLog === false));
}));

test('converter identity publishes only an opaque digest and detects source changes', async () => temporary(async directory => {
  const inventory = path.join(directory, 'inventory.json');
  await fs.writeFile(path.join(directory, 'codec.go'), 'first');
  await fs.writeFile(path.join(directory, 'model.go'), 'model');
  await fs.writeFile(inventory, JSON.stringify(['codec.go', 'model.go']));
  const first = await converterIdentity(directory, inventory);
  assert.deepEqual(Object.keys(first), ['sha256']);
  assert.match(first.sha256, /^[a-f0-9]{64}$/);
  await fs.writeFile(inventory, JSON.stringify(['model.go', 'codec.go']));
  assert.deepEqual(await converterIdentity(directory, inventory), first);
  await fs.writeFile(path.join(directory, 'codec.go'), 'changed');
  assert.notDeepEqual(await converterIdentity(directory, inventory), first);
  await fs.writeFile(inventory, JSON.stringify(['../outside.go']));
  await assert.rejects(converterIdentity(directory, inventory), /Unsafe source path/);
}));

test('parity exercises absent keys, omits converted-away flags and compares SDK result fields', async () => temporary(async directory => {
  const generatedDirectory = path.join(root, 'rules-resources/generated/canonical-evaluator');
  const binary = await fs.readFile(path.join(generatedDirectory, 'ufc.pb'));
  const wire = await fs.readFile(path.join(generatedDirectory, 'configuration-wire.json'), 'utf8');
  const source = { flags: { omitted: {} } };
  const generated = { flags: {} };
  const missing = { flag: 'missing', variationType: 'BOOLEAN', defaultValue: false,
    attributes: {}, targetingKey: 'subject', result: { value: false, reason: 'DEFAULT', errorCode: 'FLAG_NOT_FOUND', features: ['not exposed'] } };
  await fs.mkdir(path.join(directory, 'evaluation-cases'));
  await fs.writeFile(path.join(directory, 'evaluation-cases/coverage.json'), JSON.stringify([
    missing, { ...missing, flag: 'omitted' }, { ...missing, result: { value: true } },
  ]));
  const report = parity(directory, binary, wire, generated, source);
  assert.equal(report.exercisedCases, 2);
  assert.deepEqual(report.omittedCases.map(row => row.flag), ['omitted']);
  assert.ok(report.results.every(row => row.mismatches.every(m => m.field !== 'features')));
  assert.deepEqual(report.results[1].mismatches.map(m => m.encoding), ['original', 'generated', 'binary', 'wire']);
  assert.ok(report.results[1].mismatches.every(m => m.field === 'value'));
  assert.ok(report.results[0].mismatches.every(m => m.field !== 'errorCode'));
}));
