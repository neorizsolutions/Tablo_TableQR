import { test } from 'node:test';
import assert from 'node:assert/strict';
import { confirmAction, confirmClose, getConfirmation, finishConfirmation } from '../src/lib/confirmation.ts';

test('confirmation cancellation prevents close; acceptance closes exactly once', async () => {
  let closed = 0;
  const cancelled = confirmClose(() => closed++);
  assert.equal(getConfirmation().confirmLabel, 'Close window');
  finishConfirmation(false);
  await cancelled;
  assert.equal(closed, 0);
  const accepted = confirmClose(() => closed++);
  finishConfirmation(true);
  await accepted;
  assert.equal(closed, 1);
  assert.equal(getConfirmation(), null);
});

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
