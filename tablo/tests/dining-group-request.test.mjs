import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import ts from 'typescript';

test('dining group request includes the caller operation ID for safe retries', async () => {
  const calls = [];
  globalThis.__diningRequestTest = {
    apiClient: async (path, options) => { calls.push({ path, ...options }); return { id: 'group-a', label: 'A' }; },
    API_BASE_URL: 'http://test.invalid/api',
  };
  try {
    const source = readFileSync(new URL('../src/services/service-desk.ts', import.meta.url), 'utf8');
    const { outputText } = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext } });
    const code = `const {apiClient,API_BASE_URL}=globalThis.__diningRequestTest;\n${outputText.replace(/^import .*;\r?$/gm, '')}`;
    const { createDiningGroup } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`);
    const operationId = '84ac4b4f-cd39-4ac1-b495-97b4b11c7091';
    const result = await createDiningGroup('restaurant', 'table', 'A', operationId);
    await createDiningGroup('restaurant', 'table', 'A', operationId);
    assert.equal(result.id, 'group-a');
    assert.equal(calls[0].path, '/restaurants/restaurant/service-desk/tables/table/groups');
    assert.equal(calls[0].method, 'POST');
    assert.deepEqual(JSON.parse(calls[0].body), { label: 'A', operationId });
    assert.equal(calls[1].body, calls[0].body);
  } finally {
    delete globalThis.__diningRequestTest;
  }
});
