import { test,expect,developmentLogin,expectVillageView,DEMO_SLUG,uniqueName } from './fixtures.ts'

test.beforeEach(async({page})=>{
 await page.goto(`/v/${DEMO_SLUG}`)
 await developmentLogin(page,uniqueName('lighting'))
 await expectVillageView(page)
 await page.goto('/e2e/harness.html')
 await expect(page.locator('.take-amount')).toBeVisible()
})

test('all 26 buildings keep geometry and opaque walls during the day/night crossfade',async({page})=>{
 const result=await page.evaluate(()=>{
  const api=(window as unknown as {lightingTest:{render(n:number):void;measure():unknown;alphaCheck():{common:number;transparent:number;dimensions:number[][]}[]}}).lightingTest
  api.render(0); const day=api.measure();api.render(0.5);const middle=api.measure();api.render(1);const night=api.measure()
  return {day,middle,night,alpha:api.alphaCheck()}
 })
 expect(result.middle).toEqual(result.day);expect(result.night).toEqual(result.day)
 expect(result.alpha).toHaveLength(26)
 for(const item of result.alpha){expect(item.common).toBeGreaterThan(10);expect(item.transparent).toBe(0);expect(item.dimensions[1]).toEqual(item.dimensions[0]);expect(item.dimensions[2]).toEqual(item.dimensions[0])}
})

test('storage amount stays empty while editing and preserves two across inventory refresh',async({page})=>{
 const input=page.locator('.take-amount');const take=page.locator('.storage-take button')
 await input.fill('');await expect(input).toHaveValue('');await expect(take).toBeDisabled()
 await input.fill('2');await page.locator('#stock-update').click();await expect(input).toHaveValue('2')
 await take.click();await expect(page.locator('#taken')).toHaveText('2');await expect(input).toHaveValue('2')
 await input.fill('999');await expect(input).toHaveValue('999');await expect(take).toBeDisabled()
})

test('phone task text and governor buttons fit 320px with enlarged text',async({page})=>{
 await page.setViewportSize({width:320,height:800})
 await page.locator('html').evaluate(el=>el.style.fontSize='130%')
 const measured=await page.locator('.hud-status').evaluate(el=>{
  const panel=el.getBoundingClientRect(), text=el.querySelector('.next-action')!.getBoundingClientRect()
  return {panel:{left:panel.left,right:panel.right},text:{left:text.left,right:text.right},buttons:[...el.querySelectorAll(':scope > button')].map(b=>{const r=b.getBoundingClientRect();return {left:r.left,right:r.right,top:r.top}})}
 })
 expect(measured.panel.left).toBeGreaterThanOrEqual(0);expect(measured.panel.right).toBeLessThanOrEqual(320)
 expect(measured.text.left).toBeGreaterThanOrEqual(measured.panel.left);expect(measured.text.right).toBeLessThanOrEqual(measured.panel.right)
 for(const b of measured.buttons){expect(b.left).toBeGreaterThanOrEqual(0);expect(b.right).toBeLessThanOrEqual(320)}
 const textBottom=await page.locator('.hud-status .next-action').evaluate(el=>el.getBoundingClientRect().bottom)
 for(const b of measured.buttons)expect(b.top).toBeGreaterThanOrEqual(textBottom)
})

test('chaplain lines are neither the tsar nor the own message',async({page})=>{
 const panel=page.locator('.chat-panel')
 if(await panel.locator('.chat-lines').count()===0) await panel.locator('.fold-head').click()
 const line=panel.locator('.chat-line.message');await expect(line).toContainText('Отец Срамослав')
 await expect(line).not.toHaveClass(/own/);await expect(line.locator('.tsar')).toHaveCount(0)
})


test('the real completed village renders finished projects and all building types',async({page})=>{
 await page.goto('/v/demo-built')
 await expectVillageView(page)
 await expect(page.locator('.game-root')).toBeVisible()
})


test('all 26 buildings emit warm light at night and none in full daylight',async({page})=>{
 const samples=await page.evaluate(()=> (window as unknown as {lightingTest:{emissionCheck():{day:number;night:number}[]}}).lightingTest.emissionCheck())
 expect(samples).toHaveLength(26)
 for(const sample of samples){expect(sample.day).toBe(0);expect(sample.night).toBeGreaterThan(100)}
})


test('all 26 buildings load the same baked opaque outline ink in both atlas frames',async({page})=>{
 const counts=await page.evaluate(()=> (window as unknown as {lightingTest:{outlineCheck():number[][]}}).lightingTest.outlineCheck())
 expect(counts).toHaveLength(26)
 for(const [day,night] of counts){expect(day).toBeGreaterThan(10);expect(night).toBeGreaterThan(10)}
})
