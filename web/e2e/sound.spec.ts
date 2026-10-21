import { expect, test } from '@playwright/test'

test('village music loads one calm track and switches to the battle theme during a raid', async ({ page }) => {
  const files: string[] = []
  page.on('request', (request) => {
    const name = request.url().split('?')[0]?.split('/').pop() ?? ''
    if (name.endsWith('.mp3')) files.push(name)
  })
  await page.goto('/e2e/sound-harness.html')
  await page.waitForFunction(() => 'villageMusic' in window)

  const rules = await page.evaluate(() => {
    const stand = window as unknown as {
      villageMusic: { cityRaidPlaying: (battles: { finishedAt: string | null; phase: string }[]) => boolean }
    }
    const playing = stand.villageMusic.cityRaidPlaying
    return {
      warning: playing([{ finishedAt: null, phase: 'Warning' }]),
      approaching: playing([{ finishedAt: null, phase: 'Approaching' }]),
      fighting: playing([{ finishedAt: null, phase: 'Fighting' }]),
      retreating: playing([{ finishedAt: null, phase: 'Retreating' }]),
      finished: playing([{ finishedAt: '2026-10-18T00:00:00Z', phase: 'Finished' }]),
      cancelled: playing([{ finishedAt: null, phase: 'Cancelled' }]),
    }
  })
  expect(rules).toEqual({
    warning: false,
    approaching: true,
    fighting: true,
    retreating: true,
    finished: false,
    cancelled: false,
  })

  const toggle = page.getByRole('button', { name: /Village music|Turn village music on|Музыка деревни|Включить музыку деревни/ })
  const box = await toggle.boundingBox()
  const viewport = page.viewportSize()
  expect(box).not.toBeNull()
  expect(viewport).not.toBeNull()
  expect(box!.x).toBeGreaterThanOrEqual(0)
  expect(box!.y).toBeGreaterThanOrEqual(0)
  expect(box!.x + box!.width).toBeLessThanOrEqual(viewport!.width)
  expect(box!.y + box!.height).toBeLessThanOrEqual(viewport!.height)

  await toggle.click()
  await expect.poll(() => page.evaluate(() => {
    const stand = window as unknown as { villageMusic: { snapshot: () => { src: string; paused: boolean; loop: boolean } } }
    return stand.villageMusic.snapshot()
  })).toMatchObject({ paused: false, loop: false })
  const calm = await page.evaluate(() => {
    const stand = window as unknown as { villageMusic: { snapshot: () => { src: string } } }
    return stand.villageMusic.snapshot().src
  })
  expect(calm).toMatch(/part_00\d\.mp3$/)

  await page.getByRole('button', { name: 'raid' }).click()
  await expect.poll(() => page.evaluate(() => {
    const stand = window as unknown as { villageMusic: { snapshot: () => { src: string; loop: boolean } } }
    return stand.villageMusic.snapshot()
  })).toMatchObject({ loop: true })
  await expect.poll(() => page.evaluate(() => {
    const stand = window as unknown as { villageMusic: { snapshot: () => { src: string } } }
    return stand.villageMusic.snapshot().src
  })).toMatch(/battle\.mp3$/)

  const during = [...files]
  expect(new Set(during.filter((name) => name.startsWith('part_'))).size).toBe(1)
  expect(during).toContain('battle.mp3')

  await page.getByRole('button', { name: 'raid' }).click()
  await expect.poll(() => page.evaluate(() => {
    const stand = window as unknown as { villageMusic: { snapshot: () => { src: string } } }
    return stand.villageMusic.snapshot().src
  })).toBe(calm)
  expect(new Set(files.filter((name) => name.startsWith('part_'))).size).toBe(1)

  await page.getByRole('button', { name: /Village music|Музыка деревни/ }).click()
  await expect.poll(() => page.evaluate(() => {
    const stand = window as unknown as { villageMusic: { snapshot: () => { paused: boolean } } }
    return stand.villageMusic.snapshot().paused
  })).toBe(true)
})


type Probe = { elements: number; contexts: number; volume: number; ratio: number; input: number }
type Music = { src: string; paused: boolean; scene: string; outputVolume: number; volumeBackend: string; contextState: string | null }
const readMusic = (page: import('@playwright/test').Page) => page.evaluate(() =>
  (window as unknown as { villageMusic: { snapshot(): Music } }).villageMusic.snapshot())
const readProbe = (page: import('@playwright/test').Page) => page.evaluate(() =>
  (window as unknown as { musicProbe(): Probe }).musicProbe())

async function probeAudio(page: import('@playwright/test').Page) {
  await page.addInitScript(() => {
    const elements: HTMLAudioElement[] = []
    const contexts = new Set<AudioContext>()
    let before: AnalyserNode | undefined, after: AnalyserNode | undefined
    let gain: GainNode | undefined
    const NativeAudio = window.Audio
    window.Audio = class extends NativeAudio {
      constructor(src?: string) { super(src); elements.push(this) }
    }
    const makeSource = AudioContext.prototype.createMediaElementSource
    AudioContext.prototype.createMediaElementSource = function (audio) {
      const source = makeSource.call(this, audio)
      before = this.createAnalyser(); before.fftSize = 2048
      source.connect(before)
      return source
    }
    const makeGain = AudioContext.prototype.createGain
    AudioContext.prototype.createGain = function () {
      contexts.add(this)
      gain = makeGain.call(this)
      after = this.createAnalyser(); after.fftSize = 2048
      gain.connect(after)
      return gain
    }
    const rms = (node?: AnalyserNode) => {
      if (!node) return 0
      const data = new Float32Array(node.fftSize)
      node.getFloatTimeDomainData(data)
      return Math.sqrt(data.reduce((sum, v) => sum + v * v, 0) / data.length)
    }
    ;(window as unknown as { musicProbe(): Probe }).musicProbe = () => {
      const input = rms(before), output = rms(after)
      return { elements: elements.length, contexts: contexts.size, volume: gain?.gain.value ?? -1,
        input, ratio: input > 0.00001 ? output / input : -1 }
    }
  })
}

test('volume slider changes decoded audio output, 0% stops it, and one graph survives scene changes and remount', async ({ page }, testInfo) => {
  await probeAudio(page)
  await page.goto('/e2e/sound-harness.html')
  const slider = page.getByRole('slider', { name: /Music volume|Громкость музыки/ })
  await slider.focus()
  await slider.press('Home')
  for (let n = 0; n < 5; n++) await slider.press('ArrowRight')
  await expect(slider).toHaveValue('25')
  await expect.poll(() => readMusic(page)).toMatchObject({ paused: false, volumeBackend: 'web-audio', contextState: 'running' })
  await expect.poll(async () => (await readProbe(page)).input).toBeGreaterThan(0.0001)
  await expect.poll(async () => (await readProbe(page)).ratio).toBeCloseTo(0.25, 2)
  await testInfo.attach('decoded-audio-at-25-percent', { body: JSON.stringify(await readProbe(page)), contentType: 'application/json' })
  const track = (await readMusic(page)).src
  await slider.press('End')
  for (let n = 0; n < 5; n++) await slider.press('ArrowLeft')
  await expect(slider).toHaveValue('75')
  await expect.poll(async () => (await readProbe(page)).ratio).toBeCloseTo(0.75, 2)
  await testInfo.attach('decoded-audio-at-75-percent', { body: JSON.stringify(await readProbe(page)), contentType: 'application/json' })
  expect((await readMusic(page)).src).toBe(track)
  const enabled = page.getByRole('checkbox', { name: /Background music|Фоновая музыка/ })
  await enabled.uncheck()
  await expect.poll(() => readMusic(page)).toMatchObject({ paused: true })
  await enabled.check()
  await expect.poll(async () => (await readProbe(page)).ratio).toBeCloseTo(0.75, 2)
  await page.getByRole('combobox', { name: 'Music context' }).selectOption('farmDay')
  await page.getByRole('combobox', { name: 'Music context' }).selectOption('M07')
  await page.getByRole('combobox', { name: 'Music context' }).selectOption('M09')
  await page.getByRole('button', { name: 'remount', exact: true }).click()
  await expect.poll(() => readMusic(page)).toMatchObject({ scene: 'M09', src: track, paused: false })
  await expect.poll(async () => (await readProbe(page)).ratio).toBeCloseTo(0.75, 2)
  expect(await readProbe(page)).toMatchObject({ elements: 1, contexts: 1 })
  await page.getByRole('combobox', { name: 'Music context' }).selectOption('M11')
  await expect.poll(() => readMusic(page)).toMatchObject({ scene: 'M11', paused: false })
  await expect.poll(async () => (await readProbe(page)).ratio).toBeCloseTo(0.75, 2)
  await slider.focus(); await slider.press('Home')
  await expect.poll(() => readMusic(page)).toMatchObject({ paused: true })
  await expect.poll(async () => (await readProbe(page)).volume).toBeCloseTo(0, 3)
  await testInfo.attach('decoded-audio-at-zero-percent', { body: JSON.stringify({ ...await readProbe(page), paused: (await readMusic(page)).paused }), contentType: 'application/json' })
  await page.getByRole('button', { name: 'remount', exact: true }).click()
  await expect.poll(() => readMusic(page)).toMatchObject({ paused: true })
  await page.getByRole('button', { name: 'leave / return', exact: true }).click()
  await page.getByRole('button', { name: 'leave / return', exact: true }).click()
  await expect.poll(() => readMusic(page)).toMatchObject({ paused: true })
  await slider.focus(); await slider.press('ArrowRight')
  await expect(slider).toHaveValue('5')
  await expect.poll(async () => (await readProbe(page)).ratio).toBeCloseTo(0.05, 2)
  await page.reload()
  await expect(slider).toHaveValue('5')
  await expect.poll(() => readMusic(page)).toMatchObject({ paused: true })
  await page.getByRole('button', { name: /Village music|Turn village music on|Музыка деревни|Включить музыку деревни/ }).click()
  await expect.poll(async () => (await readProbe(page)).ratio).toBeCloseTo(0.05, 2)
  expect(await readProbe(page)).toMatchObject({ elements: 1, contexts: 1 })
})

test('touch or mouse slider still controls the signal when media-element volume setters are ignored', async ({ page, isMobile }) => {
  await probeAudio(page)
  await page.addInitScript(() => {
    Object.defineProperty(HTMLMediaElement.prototype, 'volume', { configurable: true, get: () => 1, set: () => undefined })
  })
  await page.goto('/e2e/sound-harness.html')
  const slider = page.getByRole('slider', { name: /Music volume|Громкость музыки/ })
  const box = (await slider.boundingBox())!
  const x = box.x + box.width * 0.3, y = box.y + box.height / 2
  if (isMobile) await page.touchscreen.tap(x, y)
  else await page.mouse.click(x, y)
  const expected = Number(await slider.inputValue()) / 100
  expect(expected).toBeGreaterThan(0.15)
  expect(expected).toBeLessThan(0.45)
  await expect.poll(() => readMusic(page)).toMatchObject({ paused: false, volumeBackend: 'web-audio', contextState: 'running' })
  await expect.poll(async () => (await readProbe(page)).ratio).toBeCloseTo(expected, 2)
  expect(await readProbe(page)).toMatchObject({ elements: 1, contexts: 1 })
})

test('unavailable Web Audio retains the existing native volume fallback and mute', async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(window, 'AudioContext', { configurable: true, value: undefined })
    Object.defineProperty(window, 'webkitAudioContext', { configurable: true, value: undefined })
  })
  await page.goto('/e2e/sound-harness.html')
  const slider = page.getByRole('slider', { name: /Music volume|Громкость музыки/ })
  await slider.focus(); await slider.press('End')
  for (let n = 0; n < 10; n++) await slider.press('ArrowLeft')
  await expect(slider).toHaveValue('50')
  await expect.poll(() => readMusic(page)).toMatchObject({ paused: false, outputVolume: 0.5, volumeBackend: 'media-element' })
  await slider.press('Home')
  await expect.poll(() => readMusic(page)).toMatchObject({ paused: true, outputVolume: 0 })
})
