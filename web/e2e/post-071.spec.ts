import { test,expect,uniqueName } from './fixtures.ts'
const wallet={coins:123,testCandles:4,workPay:10,constructionPay:20,inventory:[],stockPrices:{Wood:2},residents:[],lots:[],exchangeRate:100,dailyExchangeLimit:5,exchangedToday:0,kingdomCandleRoom:50,dayResetsAt:'2026-10-17T00:00:00Z',earnedToday:0,profitToday:0,taxPreview:0,privateProperty:[],privateStorages:[],journal:[],nextJournalBefore:null,startedAt:'2026-10-16T00:00:00Z'}

test('Tsar can attack and close control is centered and touch sized',async({page})=>{
 await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
 await page.route('**/api/villages/stand/economy?*',r=>r.fulfill({json:wallet}))
 let commands=0
 await page.route('**/battles/raid/order',async r=>{commands++;expect(r.request().postDataJSON()).toMatchObject({targetUnitId:'target',mode:'intercept'});await r.fulfill({status:200})})
 await page.goto('/e2e/post-071-harness.html')
 await expect.poll(()=>commands).toBe(1);await expect(page.getByRole('status')).toContainText('Цель выбрана')
 const close=page.locator('.battle-controls .close'),box=await close.boundingBox(),icon=await close.locator('svg').boundingBox()
 expect(box!.width).toBeGreaterThanOrEqual(44);expect(box!.height).toBeGreaterThanOrEqual(44)
 expect(Math.abs(icon!.x+icon!.width/2-box!.x-box!.width/2)).toBeLessThan(1)
 expect(Math.abs(icon!.y+icon!.height/2-box!.y-box!.height/2)).toBeLessThan(1)
 await close.click();await expect(page.locator('.battle-controls')).toHaveCount(0);expect(commands).toBe(1)
})
test('application actions follow the copy without a giant blank region; wallet shows personal test balances',async({page})=>{
 await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
 await page.route('**/api/villages/stand/economy?*',r=>r.fulfill({json:wallet}))
 await page.goto('/e2e/post-071-harness.html')
 await page.locator('.battle-controls .close').click()
 await page.locator('.civic-applications summary').click()
 const copy=await page.locator('.civic-order>span').boundingBox(),action=await page.locator('.civic-order .row').boundingBox()
 expect(action!.y-copy!.y-copy!.height).toBeLessThan(24)
 await expect(page.locator('.wallet-hud')).toContainText('123');await expect(page.locator('.wallet-hud')).toContainText('Золото');await expect(page.locator('.wallet-hud')).toContainText('4');await expect(page.locator('.wallet-hud')).toContainText('тест')
 await page.locator('.wallet-hud').click();await expect(page.getByRole('dialog',{name:'Кошелёк',exact:true})).toBeVisible();await expect(page.locator('.economy-balances')).toContainText('123');await page.getByRole('button',{name:'Вернуться к карте'}).click();await expect(page.getByRole('dialog',{name:'Кошелёк',exact:true})).toHaveCount(0)
 await page.screenshot({path:`test-results/post-071-${test.info().project.name}.png`,fullPage:true})
})

test('resource icons and wallet fit the existing HUD on phone and desktop',async({page})=>{
 await page.request.post('/auth/dev-login',{data:{name:uniqueName('wallet-layout')}})
 await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
 await page.route('**/api/villages/stand/economy?*',r=>r.fulfill({json:{...wallet,coins:1234567,testCandles:9999}}))
 for(const width of [320,390,430,960]){
  await page.setViewportSize({width,height:844})
  await page.goto('/e2e/post-071-harness.html?hud=1')
  const balances=page.locator('.hud-resources .wallet-hud')
  await expect(balances).toContainText('тест')
  const box=await balances.boundingBox(),weather=await page.locator('.weather-chip').boundingBox()
  expect(box!.x).toBeGreaterThanOrEqual(0);expect(box!.x+box!.width).toBeLessThanOrEqual(width)
  expect(Math.abs(box!.y-weather!.y)).toBeLessThan(3)
  for(const amount of await page.locator('.hud-resource-amount').all())expect(await amount.evaluate(el=>el.scrollWidth<=el.clientWidth)).toBe(true)
  const icons=await page.locator('.hud-resource-icon').evaluateAll(slots=>slots.map(slot=>{
   const rect=slot.getBoundingClientRect(),icon=slot.querySelector('.tile-icon')!.getBoundingClientRect()
   const amount=slot.parentElement!.querySelector('.hud-resource-amount')!.getBoundingClientRect()
   return {width:icon.width,height:icon.height,left:icon.left-rect.left,top:icon.top-rect.top,right:icon.right,amountLeft:amount.left}
  }))
  expect(icons).toHaveLength(6)
  const size=width<=720?16:24
  for(const icon of icons){
   expect(icon.width).toBeCloseTo(size,2);expect(icon.height).toBeCloseTo(size,2)
   expect(icon.left).toBeGreaterThanOrEqual(-.1);expect(icon.top).toBeGreaterThanOrEqual(-.1)
   expect(icon.top+icon.height).toBeLessThanOrEqual(size+.1)
   expect(icon.right).toBeLessThanOrEqual(icon.amountLeft+.1)
  }
  await page.screenshot({path:test.info().outputPath(`wallet-${width}.png`)})
 }
})
