import test from 'node:test';
import assert from 'node:assert/strict';

test('the TypeScript/browser lane can execute compiled test evidence', () => {
  assert.equal('client ready', 'client ready');
});
