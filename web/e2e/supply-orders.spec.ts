import {test,expect} from './fixtures.ts'

for(const language of ['ru','en']) {
 test(`supply publication retry retains fund terms and stock reserve controls fit (${language})`,async({page})=>{
  await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
  await page.setViewportSize({width:320,height:760})
  await page.goto('/e2e/economy-harness.html?supply=owner&tsar=1')
  await page.locator('.economy-panel > summary').click();await page.locator('.supply-orders > summary').click()
  await page.getByText(language==='ru'?'Заказать материалы':'Request supplies',{exact:true}).click()
  await page.getByLabel(language==='ru'?'Нужно единиц':'Units needed',{exact:true}).fill('2')
  await page.getByLabel(language==='ru'?'Цена за единицу':'Price per unit',{exact:true}).fill('5')
  await page.evaluate(()=>{window.failEconomyOnce=true})
  const publish=page.getByRole('button',{name:language==='ru'?'Опубликовать поставку':'Publish supply order',exact:true})
  await publish.click();await expect(page.getByRole('alert')).toBeVisible()
  await publish.click();await expect(page.getByRole('alert')).toHaveCount(0)
  const calls=await page.evaluate(()=>window.economyCalls.filter(c=>c.action==='publish-supply-order'))
  expect(calls).toHaveLength(2);expect(calls[0]).toEqual(calls[1])
  await expect(page.locator('.supply-orders')).toContainText(language==='ru'?'Свободная казна: 90':'Available treasury: 90')
  await expect(page.locator('.supply-order input')).toHaveCount(0)
  await expect(page.locator('.supply-order button')).toHaveText(language==='ru'?'Закрыть поставку':'Close supply order')
  await page.getByText(language==='ru'?'Сохранить общий запас':'Protect public stock',{exact:true}).click()
  await page.getByRole('combobox',{name:language==='ru'?'Защищаемый ресурс':'Protected resource',exact:true}).selectOption('Stone')
  await page.getByLabel(language==='ru'?'Оставлять не меньше':'Keep at least',{exact:true}).fill('8')
  await page.getByRole('button',{name:language==='ru'?'Сохранить остаток':'Save stock reserve',exact:true}).click()
  expect(await page.evaluate(()=>window.economyCalls.at(-1))).toMatchObject({action:'set-trade-reserve',resource:'Stone',amount:8})
  for(const width of [320,1280])for(const theme of ['light','dark']){
   await page.setViewportSize({width,height:900});await page.evaluate(t=>document.documentElement.dataset.uiTheme=t,theme)
   expect(await page.locator('.supply-orders').evaluate(el=>el.scrollWidth<=el.clientWidth)).toBe(true)
   for(const button of await page.locator('.supply-orders button').all()){
    const box=await button.boundingBox();if(box){expect(box.x).toBeGreaterThanOrEqual(0);expect(box.x+box.width).toBeLessThanOrEqual(width)}
   }
  }
  await page.setViewportSize({width:320,height:760})
  await page.screenshot({path:test.info().outputPath(`supply-owner-${language}.png`)})
 })
 test(`delivery uses personal quantity and public buyback is visibly unavailable (${language})`,async({page})=>{
  await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
  await page.goto('/e2e/economy-harness.html?supply=worker')
  await page.locator('.economy-panel > summary').click();await page.locator('.supply-orders > summary').click()
  await expect(page.getByText(language==='ru'?'Заказать материалы':'Request supplies',{exact:true})).toHaveCount(0)
  await expect(page.getByRole('button',{name:language==='ru'?/Купить у царства/:/Buy from kingdom/})).toBeDisabled()
  await expect(page.locator('.economy-panel')).toContainText(language==='ru'?'царство закупает этот ресурс':'the kingdom is buying this resource')
  const amount=page.getByLabel(language==='ru'?'Из личных запасов':'From personal goods',{exact:true})
  await amount.fill('5');await expect(page.locator('.supply-order button').first()).toBeDisabled()
  await amount.fill('2');await page.locator('.supply-order button').first().click()
  await expect(page.locator('.economy-balances')).toContainText('1006')
  await expect(page.locator('.supply-order-title')).toContainText('2/4')
  expect(await page.evaluate(()=>window.economyCalls.at(-1))).toMatchObject({action:'fulfill-supply-order',target:'22222222-2222-4222-8222-222222222222',resource:'Wood',amount:2,price:3})
  await page.screenshot({path:test.info().outputPath(`supply-worker-${language}.png`)})
 })
}
