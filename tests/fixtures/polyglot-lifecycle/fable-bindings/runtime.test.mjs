import assert from 'node:assert/strict';
import { greeting } from './dist/Bindings.js';

assert.equal(greeting('Fable'), 'Hello, Fable!');
console.log('Fable bindings runtime evidence passed');
