import {test,expect,uniqueName} from './fixtures.ts'

for(const language of ['en','ru']) {
 test(`a resident can inspect shared beds but needs an appointment to work (${language})`,async({page},info)=>{
  await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
  await page.setViewportSize({width:info.project.name==='phone'?320:960,height:760})
  expect((await page.request.post('/auth/dev-login',{data:{name:uniqueName('farm-access-ui')}})).status()).toBe(204)
  await page.goto('/e2e/farm-harness.html?admission=1')
  await expect(page.getByText(language==='ru'?'Для работы нужен допуск фермера.':'A farmer appointment is required.',{exact:false})).toBeVisible()
  await page.getByRole('button',{name:language==='ru'?'Грядка 1':'Bed 1',exact:true}).click()
  await expect(page.getByRole('status')).toContainText(language==='ru'?'созрело':'ripe')
  const tools=page.getByRole('toolbar')
  for(const label of language==='ru'?['Семена','Лейка','Сбор']:['Seeds','Water','Harvest'])await expect(tools.getByRole('button',{name:label,exact:true})).toBeDisabled()
  expect(await page.evaluate(()=>window.farmCalls)).toHaveLength(0)
 })

 test(`delegated manager appoints a farmer without political controls (${language})`,async({page},info)=>{
  await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
  await page.setViewportSize({width:info.project.name==='phone'?320:960,height:760})
  expect((await page.request.post('/auth/dev-login',{data:{name:uniqueName('farm-manager-ui')}})).status()).toBe(204)
  const payloads:Record<string,unknown>[]=[]
  await page.route('**/citizens/*/farm-access',route=>{const body=route.request().postDataJSON();payloads.push(body);return route.fulfill({json:{id:'10000000-0000-4000-8000-000000000001',name:'Farmer',isGovernor:true,isFarmer:body.isFarmer,canManageFarm:false,subtitle:null}})})
  await page.goto('/e2e/farm-harness.html?manage=deputy')
  await expect(page.getByRole('textbox')).toHaveCount(0)
  const check=page.getByRole('checkbox',{name:language==='ru'?'Назначен фермером':'Appointed farmer'})
  await check.click();await expect(check).toBeChecked()
  expect(payloads).toHaveLength(1);expect(payloads[0]).toMatchObject({isFarmer:true});expect(payloads[0]).not.toHaveProperty('canManageFarm')
  expect(payloads[0].requestId).toMatch(/^[0-9a-f-]{36}$/i)
  await expect(page.getByRole('checkbox')).toHaveCount(1)
  expect((await check.locator('..').boundingBox())!.height).toBeGreaterThanOrEqual(44)
  await page.screenshot({path:test.info().outputPath(`farm-manager-${language}.png`)})
 })

 test(`owner delegates management and explicitly limits shared farm work (${language})`,async({page},info)=>{
  await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
  await page.setViewportSize({width:info.project.name==='phone'?320:960,height:760})
  expect((await page.request.post('/auth/dev-login',{data:{name:uniqueName('farm-owner-ui')}})).status()).toBe(204)
  const requests:Record<string,unknown>[]=[]
  await page.route('**/citizens/*/farm-access',route=>{const body=route.request().postDataJSON();requests.push(body);return route.fulfill({json:{id:'10000000-0000-4000-8000-000000000001',name:'Farmer',isGovernor:true,isFarmer:false,canManageFarm:body.canManageFarm,subtitle:null}})})
  await page.route('**/farm/admission',route=>{requests.push(route.request().postDataJSON());return route.fulfill({json:{farmerAdmissionEnabled:true}})})
  await page.goto('/e2e/farm-harness.html?manage=owner')
  const manager=page.getByRole('checkbox',{name:language==='ru'?'Разрешить назначать фермеров':'May appoint farmers'})
  await manager.click();await expect(manager).toBeChecked()
  const enable=page.getByRole('button',{name:language==='ru'?'Общие грядки — только назначенным фермерам':'Limit shared beds to appointed farmers'})
  await enable.click();await expect(enable).toHaveCount(0)
  expect(requests).toHaveLength(2);expect(requests[0]).toMatchObject({canManageFarm:true})
  expect(requests[1].requestId).toMatch(/^[0-9a-f-]{36}$/i)
 })
}
