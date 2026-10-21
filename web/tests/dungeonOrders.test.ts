import { test } from 'node:test'
import assert from 'node:assert/strict'
import { DungeonOrders, type DungeonOrder } from '../src/game/dungeonOrders.ts'
import { latestDungeonSnapshot } from '../src/game/dungeonSnapshot.ts'

function stand() {
  const sent: DungeonOrder[] = []
  const replies: { resolve: (value: boolean) => void; reject: (reason: Error) => void }[] = []
  const orders = new DungeonOrders((order) => {
    sent.push(order)
    return new Promise<boolean>((resolve, reject) => replies.push({ resolve, reject }))
  })
  return { orders, sent, replies }
}
const flush = () => new Promise<void>((resolve) => setImmediate(resolve))

test('a burst sends the first command and the latest intent, without a stale-click backlog', async () => {
  const s = stand()
  s.orders.offer({ method: 'Attack', args: [1] })
  for (let i = 0; i < 100; i++) s.orders.offer({ method: 'MoveTo', args: [i, i] })
  s.orders.offer({ method: 'Heal', args: ['ally'] })
  assert.deepEqual(s.sent, [{ method: 'Attack', args: [1] }])
  s.replies[0].resolve(true)
  await flush()
  assert.deepEqual(s.sent, [{ method: 'Attack', args: [1] }, { method: 'Heal', args: ['ally'] }])
  s.replies[1].resolve(true)
  await flush()
  assert.equal(s.sent.length, 2)
})

test('a rejected old target does not swallow a newer move', async () => {
  const s = stand()
  s.orders.offer({ method: 'Attack', args: [1] })
  s.orders.offer({ method: 'MoveTo', args: [4, 5] })
  s.replies[0].resolve(false)
  await flush()
  assert.deepEqual(s.sent[1], { method: 'MoveTo', args: [4, 5] })
  s.replies[1].resolve(true)
})

test('disconnect clears pending commands and never retries a lost order', async () => {
  const s = stand()
  s.orders.offer({ method: 'Attack', args: [1] })
  s.orders.offer({ method: 'Attack', args: [2] })
  s.orders.clear()
  s.replies[0].reject(new Error('disconnected'))
  await flush()
  assert.equal(s.sent.length, 1)
  s.orders.offer({ method: 'MoveTo', args: [8, 9] })
  assert.deepEqual(s.sent[1], { method: 'MoveTo', args: [8, 9] })
  s.replies[1].resolve(true)
})

test('a retreat replaces queued movement and is never replayed into a reconnected fight',async()=>{
 const s=stand();s.orders.offer({method:'Attack',args:[1]});s.orders.offer({method:'MoveTo',args:[4,5]});s.orders.offer({method:'Retreat',args:[]})
 s.replies[0].resolve(true);await flush();assert.deepEqual(s.sent[1],{method:'Retreat',args:[]})
 s.orders.suspend();s.replies[1].reject(Error('disconnected'));await flush();s.orders.resume()
 assert.equal(s.sent.length,2)
})

test('leaving a run disposes the queue even when an old acknowledgement arrives later', async () => {
  const s = stand()
  s.orders.offer({ method: 'Attack', args: [1] })
  s.orders.offer({ method: 'MoveTo', args: [1, 2] })
  s.orders.dispose()
  s.replies[0].resolve(true)
  await flush()
  s.orders.offer({ method: 'Heal', args: ['someone-in-the-next-run'] })
  assert.equal(s.sent.length, 1)
})

for (const rejected of [false, true]) test(`old session acknowledgement cannot flush or erase a new session queue (rejected=${rejected})`, async () => {
  const s = stand()
  s.orders.offer({ method: 'Attack', args: [1] })
  s.orders.offer({ method: 'Attack', args: [2] })
  s.orders.suspend()
  s.orders.offer({ method: 'Attack', args: [3] })
  s.orders.resume()
  s.orders.offer({ method: 'MoveTo', args: [4, 4] })
  s.orders.offer({ method: 'Heal', args: ['new-ally'] })
  if (rejected) s.replies[0].reject(new Error('old socket closed'))
  else s.replies[0].resolve(true)
  await flush()
  assert.equal(s.sent.length, 2)
  s.replies[1].resolve(true)
  await flush()
  assert.deepEqual(s.sent[2], { method: 'Heal', args: ['new-ally'] })
  s.replies[2].resolve(true)
})

test('a delayed old join cannot rewind elapsed combat time or replace a terminal frame', () => {
  const running = { runId: 'a', elapsed: 10, outcome: 'Pending' }
  const ended = { ...running, outcome: 'Victory' }
  assert.equal(latestDungeonSnapshot(running, { ...running, elapsed: 9 }), running)
  assert.equal(latestDungeonSnapshot(running, ended), ended)
  assert.equal(latestDungeonSnapshot(ended, running), ended)
  const other = { ...running, runId: 'b', elapsed: 0 }
  assert.equal(latestDungeonSnapshot(ended, other), other)
})
