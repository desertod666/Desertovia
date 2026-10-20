import {test,expect} from './fixtures.ts'
import type {NotificationPreferenceRequest} from '../src/notificationPolicy.ts'
declare global{interface Window{noticeSaves:NotificationPreferenceRequest[];noticeMade:{title:string;body:string;tag:string}[];noticeClosed:number;noticeReads:number;failNoticeSave:'before'|'after'|null;failNoticeRead:boolean;noticePermission:NotificationPermission}}

const accountA='11111111-1111-4111-8111-111111111111'
for(const language of ['en','ru']){
 test(`account settings retain an uncertain save and fit narrow screens (${language})`,async({page})=>{
  await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
  await page.setViewportSize({width:320,height:760});await page.goto('/e2e/notification-harness.html')
  await page.getByText(language==='ru'?'События и тихие часы':'Events and quiet hours',{exact:true}).click()
  await page.getByLabel(language==='ru'?'Не беспокоить ночью':'Use quiet hours',{exact:true}).check()
  await page.getByLabel(language==='ru'?'Часовой пояс':'Time zone',{exact:true}).fill('Europe/Oslo')
  await page.evaluate(()=>{window.failNoticeSave='after'})
  const save=page.getByRole('button',{name:language==='ru'?'Сохранить уведомления':'Save notifications',exact:true})
  await save.click();await expect(page.getByRole('alert')).toContainText('Lost response')
  await save.click();await expect(page.locator('.notification-settings').getByRole('status')).toContainText(language==='ru'?'Настройки сохранены':'Settings saved')
  const calls=await page.evaluate(()=>window.noticeSaves);expect(calls).toHaveLength(2);expect(calls[0]).toEqual(calls[1])
  for(const width of [320,1280])for(const theme of ['light','dark']){
   await page.setViewportSize({width,height:900});await page.evaluate(value=>document.documentElement.dataset.uiTheme=value,theme)
   expect(await page.locator('.notification-settings').evaluate(el=>el.scrollWidth<=el.clientWidth)).toBe(true)
   for(const input of await page.locator('.notification-settings input').all()){const box=await input.boundingBox();if(box){expect(box.x).toBeGreaterThanOrEqual(0);expect(box.x+box.width).toBeLessThanOrEqual(width)}}
  }
  await page.setViewportSize({width:320,height:760});await page.screenshot({path:test.info().outputPath(`notification-settings-${language}.png`)})
 })
}
test('legacy unbound permission does not opt a new account in; batches and replay are deduplicated',async({page})=>{
 await page.addInitScript(()=>{localStorage.setItem('desertod.language','en');localStorage.setItem('desertod.notifications','on')})
 await page.goto('/e2e/notification-harness.html?background=1')
 await page.locator('#work-one').click();expect(await page.evaluate(()=>window.noticeMade)).toEqual([])
 await page.getByRole('button',{name:'Enable on this device',exact:true}).click()
 await expect(page.getByRole('button',{name:'Disable for account',exact:true})).toBeVisible()
 await page.locator('#work-one').click();await page.locator('#work-two').click()
 await expect.poll(()=>page.evaluate(()=>window.noticeMade.length)).toBe(1)
 expect((await page.evaluate(()=>window.noticeMade))[0]!.title).toContain('2 events')
 await page.locator('#work-one').click();await page.locator('#work-two').click()
 await page.waitForTimeout(3500);expect(await page.evaluate(()=>window.noticeMade.length)).toBe(1)
 const stored=await page.evaluate(account=>localStorage.getItem(`desertod.notifications.seen.${account}`),accountA)
 expect(stored).not.toContain('Fixture only')
})
test('revocation is rechecked before dispatch, collected crops and expired warnings stay silent',async({page})=>{
 await page.addInitScript(()=>localStorage.setItem('desertod.language','en'))
 await page.goto('/e2e/notification-harness.html?background=1')
 await page.getByRole('button',{name:'Enable on this device',exact:true}).click()
 await page.locator('#farm').click();await page.locator('#collect').click();await page.locator('#stale').click()
 await page.locator('#work-one').click();await page.locator('#remote-off').click()
 await expect(page.getByRole('button',{name:'Enable on this device',exact:true})).toBeVisible({timeout:15000})
 expect(await page.evaluate(()=>window.noticeMade)).toEqual([])
})
test('foreground events and pending alerts from a different account do not leak',async({page})=>{
 await page.addInitScript(()=>localStorage.setItem('desertod.language','en'))
 await page.goto('/e2e/notification-harness.html')
 await page.getByRole('button',{name:'Enable on this device',exact:true}).click();await page.locator('#work-one').click()
 await page.waitForTimeout(3500);expect(await page.evaluate(()=>window.noticeMade)).toEqual([])
 await page.goto('/e2e/notification-harness.html?background=1')
 await expect(page.getByRole('button',{name:'Disable for account',exact:true})).toBeVisible()
 await page.locator('#work-two').click();await page.locator('#switch-account').click()
 await expect(page.getByRole('button',{name:'Enable on this device',exact:true})).toBeVisible()
 await page.waitForTimeout(3500);expect(await page.evaluate(()=>window.noticeMade)).toEqual([])
})
test('two hidden tabs share a delivery claim when browser locks are available',async({page,context})=>{
 await page.addInitScript(()=>localStorage.setItem('desertod.language','en'))
 await page.goto('/e2e/notification-harness.html?background=1');await page.getByRole('button',{name:'Enable on this device',exact:true}).click()
 const other=await context.newPage();await other.goto('/e2e/notification-harness.html?background=1')
 await expect(other.locator('.notification-settings')).toBeVisible()
 await Promise.all([page.locator('#work-one').click(),other.locator('#work-one').click()])
 await expect.poll(async()=>await page.evaluate(()=>window.noticeMade.length)+await other.evaluate(()=>window.noticeMade.length)).toBe(1)
 await page.waitForTimeout(3500)
 expect(await page.evaluate(()=>window.noticeMade.length)+await other.evaluate(()=>window.noticeMade.length)).toBe(1)
 await other.close()
})
test('denied permission stays explicit and does not construct a system notification',async({page})=>{
 await page.addInitScript(()=>localStorage.setItem('desertod.language','en'))
 await page.goto('/e2e/notification-harness.html?background=1&denied=1')
 await expect(page.getByText(/blocked/i)).toBeVisible()
 await page.locator('#work-one').click();expect(await page.evaluate(()=>window.noticeMade)).toEqual([])
})
