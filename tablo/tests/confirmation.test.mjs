import { test } from 'node:test';
import assert from 'node:assert/strict';
import { confirmAction, getConfirmation, finishConfirmation } from '../src/lib/confirmation.ts';

test('queued actions resolve separately and retain their own decision', async () => {
  const first = confirmAction({ title: 'Delete category?', description: 'First' });
  const second = confirmAction({ title: 'Deactivate kitchen?', description: 'Second' });
  assert.equal(getConfirmation().title, 'Delete category?');
  finishConfirmation(false);
  assert.equal(await first, false);
  assert.equal(getConfirmation().title, 'Deactivate kitchen?');
  finishConfirmation(true);
  assert.equal(await second, true);
  assert.equal(getConfirmation(), null);
});
