import { expect, test } from './fixtures.ts'
import type { Page, CDPSession } from '@playwright/test'
import type {} from './dungeon-camera-contract.ts'

async function open(page: Page, phone: boolean, language = 'en', ambush = false) {
  await page.setViewportSize(phone ? { width: 320, height: 900 } : { width: 1280, height: 800 })
  await page.addInitScript(lang => localStorage.setItem('desertod.language', lang), language)
  await page.goto('/e2e/dungeon-camera-harness.html' + (ambush ? '?ambush' : ''))
  await page.waitForFunction(() => !!window.dungeonCameraStand)
}
const read = (page: Page) => page.evaluate(() => window.dungeonCameraStand.read())
const orders = (page: Page) => page.evaluate(() => window.dungeonCameraOrders)
const touch = (cdp: CDPSession, type: 'touchStart' | 'touchMove' | 'touchEnd' | 'touchCancel', points: { id: number; x: number; y: number }[]) =>
  cdp.send('Input.dispatchTouchEvent', { type, touchPoints: points })

for (const language of ['en', 'ru']) test(`readable initial map and44px camera controls, ${language}`, async ({ page }, info) => {
  await open(page, info.project.name === 'phone', language)
  expect((await read(page)).tilePixels).toBeGreaterThanOrEqual(32)
  const locate = page.getByRole('button', { name: language === 'ru' ? 'Найти себя на карте' : 'Find me on the map' })
  await expect(locate).toBeVisible()
  for (const button of await page.locator('.zoom-controls button').all()) {
    const box = (await button.boundingBox())!
    expect(box.width).toBeGreaterThanOrEqual(44); expect(box.height).toBeGreaterThanOrEqual(44)
    expect(box.x).toBeGreaterThanOrEqual(0); expect(box.x + box.width).toBeLessThanOrEqual(page.viewportSize()!.width)
  }
  await locate.click(); expect(await orders(page)).toEqual([])
  await page.screenshot({ path: info.outputPath(`camera-${language}.png`) })
})

test('two real fingers zoom without sending an order; the next single tap still selects the enemy', async ({ page }, info) => {
  await open(page, info.project.name === 'phone')
  const cdp = await page.context().newCDPSession(page), before = await read(page)
  await touch(cdp, 'touchStart', [{ id: 1, x: 100, y: 330 }])
  await touch(cdp, 'touchStart', [{ id: 1, x: 100, y: 330 }, { id: 2, x: 220, y: 330 }])
  await touch(cdp, 'touchMove', [{ id: 1, x: 65, y: 330 }, { id: 2, x: 255, y: 330 }])
  await touch(cdp, 'touchEnd', [{ id: 2, x: 255, y: 330 }])
  await touch(cdp, 'touchEnd', [])
  expect((await read(page)).scale).toBeGreaterThan(before.scale * 1.3)
  expect(await orders(page)).toEqual([])
  await page.getByRole('button', { name: 'Find me on the map' }).click()
  const p = await page.evaluate(() => window.dungeonCameraStand.point(11, 8.65))
  await touch(cdp, 'touchStart', [{ id: 3, x: p.x, y: p.y }]); await touch(cdp, 'touchEnd', [])
  expect(await orders(page)).toEqual(['attack:1'])
})

test('dragging inspects the map; locate restores following without moving the hero', async ({ page }, info) => {
  await open(page, info.project.name === 'phone')
  const phone = info.project.name === 'phone', axis = phone ? 'x' : 'y'
  const cdp = await page.context().newCDPSession(page), before = await read(page)
  await touch(cdp, 'touchStart', [{ id: 1, x: 160, y: 330 }])
  await touch(cdp, 'touchMove', [{ id: 1, x: phone ? 90 : 160, y: phone ? 330 : 260 }]); await touch(cdp, 'touchEnd', [])
  expect((await read(page)).following).toBe(false); expect((await read(page))[axis]).not.toBe(before[axis])
  await page.evaluate(phone => window.dungeonCameraStand.hero(phone ? 12 : 10, phone ? 10 : 12), phone)
  const inspected = await read(page)
  await page.getByRole('button', { name: 'Find me on the map' }).click()
  const located = await read(page)
  expect(located.following).toBe(true); expect(located[axis]).not.toBe(inspected[axis])
  expect(await orders(page)).toEqual([])
})

test('cancelled multiple touches cannot attack; the following normal tap recovers', async ({ page }, info) => {
  await open(page, info.project.name === 'phone')
  const cdp = await page.context().newCDPSession(page)
  await touch(cdp, 'touchStart', [{ id: 1, x: 90, y: 330 }, { id: 2, x: 180, y: 330 }, { id: 3, x: 230, y: 330 }])
  await touch(cdp, 'touchCancel', [])
  expect(await orders(page)).toEqual([])
  const p = await page.evaluate(() => window.dungeonCameraStand.point(11, 8.65))
  await touch(cdp, 'touchStart', [{ id: 4, x: p.x, y: p.y }]); await touch(cdp, 'touchEnd', [])
  expect(await orders(page)).toEqual(['attack:1'])
})

test('camera controls stay below the header and leave the ambush enemy available to a tap', async ({ page }, info) => {
  await open(page, info.project.name === 'phone', 'en', true)
  const top = (await page.locator('.dungeon-top').boundingBox())!, controls = (await page.locator('.zoom-controls').boundingBox())!
  expect(controls.y).toBeGreaterThan(top.y + top.height)
  const p = await page.evaluate(() => window.dungeonCameraStand.point(8.5, 4.15))
  expect(await page.evaluate(p => document.elementFromPoint(p.x, p.y)?.tagName, p)).toBe('CANVAS')
  const cdp = await page.context().newCDPSession(page)
  await touch(cdp, 'touchStart', [{ id: 1, x: p.x, y: p.y }]); await touch(cdp, 'touchEnd', [])
  expect(await orders(page)).toEqual(['attack:1'])
})

test('a pinch at the left edge keeps the touched ground under its midpoint', async ({ page }) => {
  await open(page, true, 'en', true)
  const cdp = await page.context().newCDPSession(page), before = await read(page)
  const groundBefore = (160 - before.x) / before.tilePixels
  await touch(cdp, 'touchStart', [{ id: 1, x: 100, y: 400 }, { id: 2, x: 220, y: 400 }])
  await touch(cdp, 'touchMove', [{ id: 1, x: 70, y: 400 }, { id: 2, x: 250, y: 400 }])
  await touch(cdp, 'touchEnd', [])
  const after = await read(page)
  expect(after.scale).toBeGreaterThan(before.scale)
  expect((160 - after.x) / after.tilePixels).toBeCloseTo(groundBefore, 1)
  expect(await orders(page)).toEqual([])
})
