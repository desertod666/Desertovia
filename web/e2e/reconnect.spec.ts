import { test, expect, type Page, type WebSocketRoute } from '@playwright/test'

type Invocation = { type: number; target: string; invocationId: string; arguments: unknown[] }
const frame = (value: unknown) => JSON.stringify(value) + '\u001e'
const snapshot = (elapsed: number) => ({runId: 'run-a', elapsed, outcome: 'Pending'})
const view = (hero = 'hero-a', elapsed = 10) => ({run: {phase: 'Running'}, myHeroId: hero, snapshot: snapshot(elapsed)})

async function transport(page: Page, hub: string) {
  const calls: Invocation[] = []
  const sockets: WebSocketRoute[] = []
  const errors: string[] = []
  page.on('pageerror', error => errors.push(error.message))
  await page.route(`**/hubs/${hub}/negotiate?*`, route => route.fulfill({json: {
    negotiateVersion: 1, connectionId: 'test', connectionToken: 'test', availableTransports: [{transport: 'WebSockets', transferFormats: ['Text']}],
  }}))
  await page.routeWebSocket(`**/hubs/${hub}?*`, socket => {
    sockets.push(socket)
    socket.onMessage(message => {
      for (const part of String(message).split('\u001e').filter(Boolean)) {
        const data = JSON.parse(part)
        if (data.protocol) socket.send(frame({}))
        else if (data.type === 1) calls.push(data)
      }
    })
  })
  return { calls, sockets, errors, reply: (call: Invocation, result: unknown) => sockets.at(-1)!.send(frame({type: 3, invocationId: call.invocationId, result})) }
}
const wake = (page: Page) => page.evaluate(() => document.dispatchEvent(new Event('resume')))

test('dungeon refresh keeps newer live frames and fences old acknowledgements until hero confirmation', async ({page}) => {
  const t = await transport(page, 'dungeon')
  await page.goto('/e2e/reconnect-harness.html')
  await expect.poll(() => t.calls.length).toBe(1)
  t.sockets[0].send(frame({type: 1, target: 'dungeonSnapshot', arguments: [snapshot(20)]}))
  t.reply(t.calls[0], view())
  await expect(page.locator('#state')).toContainText('"elapsed":20')
  // A frame during the first join may request one coalesced refresh.
  await expect.poll(() => t.calls.filter(c => c.target === 'JoinRun').length).toBe(2)
  t.reply(t.calls.at(-1)!, view('hero-a', 20))
  await page.getByRole('button', {name: 'Attack', exact: true}).click()
  await expect.poll(() => t.calls.filter(c => c.target === 'Attack').length).toBe(1)
  const old = t.calls.at(-1)!
  await page.getByRole('button', {name: 'Heal', exact: true}).click()
  await wake(page)
  await expect.poll(() => t.calls.filter(c => c.target === 'JoinRun').length).toBe(3)
  const rejoin = t.calls.at(-1)!
  await page.getByRole('button', {name: 'Attack', exact: true}).click()
  expect(t.calls.filter(c => c.target === 'Attack').length).toBe(1)
  t.reply(rejoin, view('hero-b', 25))
  await expect(page.locator('#state')).toContainText('hero-b')
  await page.getByRole('button', {name: 'Move', exact: true}).click()
  await expect.poll(() => t.calls.filter(c => c.target === 'MoveTo').length).toBe(1)
  const move = t.calls.at(-1)!
  expect(move.arguments).toEqual(['run-a', 'hero-b', 4, 5])
  await page.getByRole('button', {name: 'Heal', exact: true}).click()
  t.reply(old, true)
  // The old reply cannot release the new connection's pending heal.
  await page.waitForTimeout(100)
  expect(t.calls.filter(c => c.target === 'Heal')).toHaveLength(0)
  t.reply(move, true)
  await expect.poll(() => t.calls.filter(c => c.target === 'Heal').length).toBe(1)
  expect(t.calls.at(-1)!.arguments).toEqual(['run-a', 'hero-b', 'ally'])
  t.reply(t.calls.at(-1)!, true)
  expect(t.errors).toEqual([])
})

test('a failed rejoin is handled and another wakeup can recover without replaying clicks', async ({page}) => {
  const t = await transport(page, 'dungeon')
  await page.goto('/e2e/reconnect-harness.html')
  await expect.poll(() => t.calls.length).toBe(1)
  t.sockets[0].send(frame({type: 3, invocationId: t.calls[0].invocationId, error: 'temporary'}))
  await expect(page.locator('#state')).toContainText('"error":true')
  await page.getByRole('button', {name: 'Attack', exact: true}).click()
  await wake(page)
  await expect.poll(() => t.calls.length).toBe(2)
  t.reply(t.calls[1], view())
  await expect(page.locator('#state')).toContainText('"error":false')
  expect(t.calls.filter(c => c.target === 'Attack')).toHaveLength(0)
  expect(t.errors).toEqual([])
})

test('a real socket reconnect discards old intents and rejoins before enabling commands', async ({page}) => {
  const t = await transport(page, 'dungeon')
  await page.goto('/e2e/reconnect-harness.html')
  await expect.poll(() => t.calls.length).toBe(1)
  t.reply(t.calls[0], view())
  await expect(page.locator('#state')).toContainText('hero-a')
  await page.getByRole('button', {name: 'Attack', exact: true}).click()
  await expect.poll(() => t.calls.length).toBe(2)
  await page.getByRole('button', {name: 'Heal', exact: true}).click()
  t.sockets[0].close({code: 1001, reason: 'test reconnect'})
  await expect.poll(() => t.sockets.length).toBe(2)
  await expect.poll(() => t.calls.filter(c => c.target === 'JoinRun').length).toBe(2)
  await page.getByRole('button', {name: 'Move', exact: true}).click()
  expect(t.calls.filter(c => c.target === 'MoveTo')).toHaveLength(0)
  t.reply(t.calls.at(-1)!, view('hero-after-reconnect', 30))
  await expect(page.locator('#state')).toContainText('hero-after-reconnect')
  await page.getByRole('button', {name: 'Move', exact: true}).click()
  await expect.poll(() => t.calls.filter(c => c.target === 'MoveTo').length).toBe(1)
  expect(t.calls.at(-1)!.arguments).toEqual(['run-a', 'hero-after-reconnect', 4, 5])
  expect(t.calls.filter(c => c.target === 'Heal')).toHaveLength(0)
  t.reply(t.calls.at(-1)!, true)
  expect(t.errors).toEqual([])
})

test('a join response from before resume cannot restore old hero authority', async ({page}) => {
  const t = await transport(page, 'dungeon')
  await page.goto('/e2e/reconnect-harness.html')
  await expect.poll(() => t.calls.length).toBe(1)
  await wake(page)
  // Let the wakeup invalidate the still pending first join.
  await page.waitForTimeout(400)
  t.reply(t.calls[0], view('old-hero'))
  await expect.poll(() => t.calls.length).toBe(2)
  await page.getByRole('button', {name: 'Attack', exact: true}).click()
  expect(t.calls.filter(c => c.target === 'Attack')).toHaveLength(0)
  t.reply(t.calls[1], view('new-hero'))
  await expect(page.locator('#state')).toContainText('new-hero')
  await page.getByRole('button', {name: 'Move', exact: true}).click()
  await expect.poll(() => t.calls.length).toBe(3)
  expect(t.calls[2].arguments).toEqual(['run-a', 'new-hero', 4, 5])
  t.reply(t.calls[2], true)
  expect(t.errors).toEqual([])
})

test('village startup and resume read authority after joining, and an older full push cannot rewind it', async ({page}) => {
  const t = await transport(page, 'village')
  const sequence: string[] = []
  let reads = 0
  const state = (n: number) => ({id: 'world', name: `world-${n}`, snapshotAt: `2026-10-20T12:00:0${n}Z`, battles: [], weather: {periodEndsAt: null}})
  await page.route('**/api/villages/test-world', route => {
    sequence.push('read')
    reads++
    return route.fulfill({json: {state: state(reads), isOwner: false, inviteCode: null, citizens: [], awayCitizenIds: []}})
  })
  await page.goto('/e2e/reconnect-harness.html?village')
  await expect.poll(() => t.calls.length).toBe(1)
  sequence.push('join'); t.reply(t.calls[0], null)
  await expect(page.locator('#state')).toContainText('world-2')
  expect(sequence).toEqual(['read', 'join', 'read'])
  await wake(page)
  await expect.poll(() => t.calls.length).toBe(2)
  // No HTTP authority read until the new subscription is acknowledged.
  expect(reads).toBe(2)
  t.reply(t.calls[1], null)
  await expect(page.locator('#state')).toContainText('world-3')
  t.sockets[0].send(frame({type: 1, target: 'villageUpdated', arguments: [state(1)]}))
  await expect(page.locator('#state')).toContainText('world-3')
  expect(t.errors).toEqual([])
})
