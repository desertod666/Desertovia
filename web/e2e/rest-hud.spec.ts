import {test,expect,uniqueName} from './fixtures.ts'

for(const language of ['ru','en'])test(`phone status with rest stays compact and keeps tap details (${language})`,async({page})=>{
 await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
 await page.request.post('/auth/dev-login',{data:{name:uniqueName('rest-hud')}})
 await page.route('**/api/villages/stand/economy?*',route=>route.fulfill({json:{coins:1234567,testCandles:4321}}))
 for(const width of [320,390,720]){
  await page.setViewportSize({width,height:760})
  await page.goto('/e2e/post-071-harness.html?hud=1&needs=1&rest=1')
  const row=page.locator('.hud-state-row')
  await expect(row.locator('.wallet-hud')).toContainText('4')
  const geometry=await row.evaluate(el=>({height:el.getBoundingClientRect().height,overflow:el.scrollWidth>el.clientWidth,boxes:[...el.children].map(c=>{const r=c.getBoundingClientRect();return {left:r.left,right:r.right,top:r.top,bottom:r.bottom}})}))
  expect(geometry.height).toBeLessThan(96);expect(geometry.overflow).toBe(false)
  for(const box of geometry.boxes){expect(box.left).toBeGreaterThanOrEqual(0);expect(box.right).toBeLessThanOrEqual(width)}
  const rest=page.locator('.safe-rest-control>button').first()
  expect((await rest.boundingBox())!.height).toBeGreaterThanOrEqual(44)
  await rest.click();await expect(page.locator('.safe-rest-control')).toHaveAttribute('data-state','Resting')
  await page.getByRole('button',{name:language==='ru'?'Правила безопасного отдыха':'Safe rest rules'}).click()
  await expect(page.getByRole('tooltip')).toContainText(language==='ru'?'Рост растений':'Plants')
  await page.keyboard.press('Escape')
  await page.locator('.weather-chip').click()
  await expect(page.locator('.weather-panel')).toBeVisible()
  await page.screenshot({path:test.info().outputPath(`rest-hud-${language}-${width}.png`)})
 }
})
