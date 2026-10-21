import {test,expect,uniqueName} from './fixtures.ts'

for(const language of ['ru','en'])test(`mortal royal profile stays folded and a lost healing acknowledgement reuses the same request (${language})`,async({page})=>{
 await page.setViewportSize({width:320,height:760})
 await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
 await page.request.post('/auth/dev-login',{data:{name:uniqueName('royal-body')}})
 const requests:string[]=[]
 await page.route('**/api/villages/stand/tsar/heal',async route=>{
  requests.push(route.request().postDataJSON().requestId)
  if(requests.length===1){await route.fulfill({status:503,json:{code:'test.retry',detail:'Try again'}});return}
  const initial=await page.evaluate(()=>structuredClone((window as unknown as {royalStand:{initial:{tsar:{injuredUntil:string|null}}}}).royalStand.initial))
  initial.tsar.injuredUntil=null;await route.fulfill({json:initial})
 })
 await page.goto('/e2e/post-071-harness.html?royalCombat=1')
 const sheet=page.locator('.royal-combat-sheet')
 await expect(sheet).not.toHaveAttribute('open','')
 await expect(sheet.locator('summary')).toContainText('32')
 await sheet.locator('summary').click()
 const button=sheet.getByRole('button',{name:language==='ru'?'Использовать лекарство':'Use remedy',exact:true})
 const box=(await button.boundingBox())!;expect(box.height).toBeGreaterThanOrEqual(44);expect(box.x+box.width).toBeLessThanOrEqual(320)
 await button.click();await expect(sheet.getByRole('alert')).toBeVisible()
 await button.click();await expect(button).toHaveCount(0);await expect(sheet.getByRole('alert')).toHaveCount(0)
 expect(requests).toHaveLength(2);expect(requests[0]).toMatch(/^[0-9a-f-]{36}$/);expect(requests[1]).toBe(requests[0])
 await page.screenshot({path:test.info().outputPath(`royal-profile-${language}-320.png`)})
})
