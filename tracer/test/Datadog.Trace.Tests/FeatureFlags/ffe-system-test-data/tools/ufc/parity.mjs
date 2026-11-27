// Diagnostics only. Mismatches never filter flags or change fixture expectations.
import { createRequire } from 'node:module';
import { isDeepStrictEqual } from 'node:util';
import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
const require = createRequire(import.meta.url);
const { evaluate, evaluateRulesBasedConfiguration } = require('@datadog/flagging-core');
const { configurationFromRulesBinary, configurationFromString } = require('@datadog/flagging-core/rules-based');
const types = { BOOLEAN: 'boolean', INTEGER: 'number', NUMERIC: 'number', STRING: 'string', JSON: 'object' };
// Compare fields exposed by the pinned evaluator API; split features are not returned.
const sdkFields = new Set(['value', 'reason', 'errorCode', 'serialId', 'doLog']);
const logger = { debug() {}, info() {}, warn() {}, error() {} };
export function parity(root, binary, wire, generated, source) {
  const configs = { binary: configurationFromRulesBinary(binary), wire: configurationFromString(wire) };
  const cases = readdirSync(resolve(root, 'evaluation-cases')).filter(x => x.endsWith('.json')).sort().flatMap(file => JSON.parse(readFileSync(resolve(root, 'evaluation-cases', file))).map((value,index) => ({id:`evaluation-cases/${file}#${index}`,value})));
  const included = new Set(Object.keys(generated.flags));
  const results = [], omittedCases = [];
  const numericWarnings = [];
  function inspectNumbers(value, path) {
    if (typeof value === 'number' && (!Number.isFinite(value) || (Number.isInteger(value) && !Number.isSafeInteger(value)))) numericWarnings.push({path, value: String(value), warning:'Outside JavaScript safe numeric range; diagnostic comparison cannot establish exact source precision'});
    if (value && typeof value === 'object') for (const [key, child] of Object.entries(value)) inspectNumbers(child, `${path}/${key}`);
  }
  inspectNumbers(generated, 'generated');
  for (const c of cases) if (included.has(c.value.flag) || !Object.hasOwn(source.flags, c.value.flag)) inspectNumbers(c.value, c.id);
  const RealDate = globalThis.Date;
  const evaluationTime = '2026-01-01T00:00:00.000Z';
  try {
    const fixedTime = RealDate.parse(evaluationTime);
    // The SDK uses both Date.now() and new Date().getTime().
    globalThis.Date = class extends RealDate {
      constructor(...args) { super(...(args.length ? args : [fixedTime])); }
      static now() { return fixedTime; }
    };
    for (const {id,value:test} of cases) {
      if (!included.has(test.flag) && Object.hasOwn(source.flags, test.flag)) { omittedCases.push({id,flag:test.flag}); continue; }
      const context = {...test.attributes,targetingKey:test.targetingKey};
      const calls = {
        original: () => evaluateRulesBasedConfiguration(source,types[test.variationType],test.flag,test.defaultValue,context,logger),
        generated: () => evaluateRulesBasedConfiguration(generated,types[test.variationType],test.flag,test.defaultValue,context,logger),
        binary: () => evaluate(configs.binary,types[test.variationType],test.flag,test.defaultValue,context,logger),
        wire: () => evaluate(configs.wire,types[test.variationType],test.flag,test.defaultValue,context,logger),
      };
      const mismatches = [];
      for (const [encoding,call] of Object.entries(calls)) {
        try {
          if (!types[test.variationType]) throw Error(`Unknown variation type ${test.variationType}`);
          const sdkResult = call();
          // Project observed SDK metadata into the corpus result shape; preserve absence.
          const actual = { ...sdkResult };
          actual.serialId = sdkResult.flagMetadata?.__dd_split_serial_id ?? null;
          actual.doLog = sdkResult.flagMetadata?.doLog;
          for (const [field,expected] of Object.entries(test.result)) if (sdkFields.has(field) && !isDeepStrictEqual(actual[field],expected)) mismatches.push({encoding,field,expected,actual:actual[field] ?? null});
        } catch(error) { mismatches.push({encoding,error:error.message}); }
      }
      results.push({id,flag:test.flag,passed:!mismatches.length,mismatches});
    }
  } finally { globalThis.Date = RealDate; }
  return {sdk:'@datadog/flagging-core@3.0.1',evaluationTime,numericWarnings,totalCases:cases.length,exercisedCases:results.length,omittedCases,failures:results.filter(x=>!x.passed),results};
}
