import test from 'node:test';
import assert from 'node:assert/strict';
import { greeting } from './dist/Bindings.js';

test('Fable bindings compile and run in Node', () => {
  assert.equal(greeting('Fable'), 'Hello, Fable!');
});
