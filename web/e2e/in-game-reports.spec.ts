import { test, expect, openMenu, uniqueName } from './fixtures.ts'
import { writeFile } from 'node:fs/promises'
import sharp from 'sharp'

test.use({serviceWorkers:'block'})

async function village(page: import('@playwright/test').Page) {
  await page.goto('/')
  // Settle initial account loading before API login/navigation, including WebKit.
  await expect(page.locator('section.login')).toBeVisible()
  await page.request.post('/auth/dev-login', {data:{name:uniqueName('report')}})
  await page.request.post('/api/me/character', {data:{name:'Report tester',appearance:{body:'Female',style:0,skinTone:0,hairColor:0,outfitColor:0}}})
  const created = await (await page.request.post('/api/villages', {data:{name:uniqueName('Report village'),isPrivate:false}})).json()
  await page.goto(`/v/${created.slug}`)
  await expect(page.locator('.hud-resources')).toBeVisible()
  await expect(page.locator('.game-canvas canvas').first()).toBeVisible()
  return created.slug as string
}
const trigger = (page: import('@playwright/test').Page) => page.getByRole('button',{name:'Сообщить об ошибке или предложить идею'})
const form = (page: import('@playwright/test').Page) => page.getByRole('dialog',{name:'Обратная связь'})

test('small symbol stays above actual menu; cancel, X and Escape discard unsent screenshots',async({page},info)=>{
  test.slow()
  await village(page);await openMenu(page)
  const writes: string[]=[]
  page.on('request',r=>{if(r.url().includes('/api/reports'))writes.push(r.method())})
  const storageBefore=await page.evaluate(()=>({local:{...localStorage},session:{...sessionStorage}}))
  await expect(trigger(page)).toHaveText('!')
  const rect=await trigger(page).boundingBox();expect(rect!.width).toBeLessThanOrEqual(36);expect(rect!.height).toBeLessThanOrEqual(36)
  await trigger(page).click();await expect(form(page)).toBeVisible()
  await expect(form(page).locator('img')).toBeVisible()
  const source=await form(page).locator('img').getAttribute('src')
  const bytes=Buffer.from(source!.split(',')[1],'base64')
  expect(bytes.length).toBeLessThanOrEqual(350000)
  const stats=await sharp(bytes).stats();expect(stats.entropy).toBeGreaterThan(2)
  await writeFile(info.outputPath('captured-menu.jpg'),bytes)
  await page.screenshot({path:info.outputPath('report-form.png')})
  await form(page).getByLabel('Описание').fill('Do not send this cancelled draft')
  await form(page).getByRole('button',{name:'Отмена',exact:true}).click()
  await expect(form(page)).toBeHidden();await expect(page.locator('.report-preview')).toHaveCount(0)
  await trigger(page).click();await expect(form(page)).toBeVisible()
  await expect(form(page).getByLabel('Описание')).toHaveValue('')
  await form(page).getByRole('button',{name:'Закрыть отчёт'}).click()
  await trigger(page).click();await expect(form(page)).toBeVisible()
  await page.keyboard.press('Escape');await expect(form(page)).toBeHidden()
  expect(writes).toEqual([])
  expect(await page.evaluate(()=>({local:{...localStorage},session:{...sessionStorage}}))).toEqual(storageBefore)
  await expect(page.getByRole('dialog',{name:'Menu',exact:true})).toBeVisible()
})

test('explicit submit includes snapshot context; retry reuses the entire original request',async({page})=>{
  test.slow();const slug=await village(page);await openMenu(page)
  const attempts: Record<string,unknown>[]=[]
  await page.route('**/api/reports',async route=>{
    attempts.push(route.request().postDataJSON())
    if(attempts.length===1)return route.abort('failed')
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({id:attempts[0].id,state:'saved'})})
  })
  await trigger(page).click();await form(page).getByLabel('Описание').fill('Menu failed')
  await form(page).getByRole('button',{name:'Отправить',exact:true}).click()
  await expect(form(page).getByRole('alert')).toBeVisible()
  await expect(form(page).getByLabel('Описание')).toBeDisabled()
  await form(page).getByRole('button',{name:'Отправить',exact:true}).click()
  await expect(page.getByRole('dialog',{name:'Отчёт сохранён'})).toBeVisible()
  expect(attempts).toHaveLength(2);expect(attempts[0]).toEqual(attempts[1])
  expect(attempts[0]).toMatchObject({slug,kind:'bug',text:'Menu failed',screen:{page:'village',menu:'menu:village',clientVersion:'0.7.14'}})
  expect(typeof attempts[0].screenshot).toBe('string')
  expect(attempts[0]).not.toHaveProperty('accountId')
  await expect(page.locator('.report-preview')).toHaveCount(0)
})

test('removing screenshot still allows submitting a suggestion to actual API',async({page})=>{
  test.slow();await village(page)
  await trigger(page).click();await form(page).getByLabel('Тип обращения').selectOption('suggestion')
  await form(page).getByLabel('Описание').fill('Test-only proposal in isolated database')
  await form(page).getByRole('button',{name:'Убрать снимок'}).click()
  const result=page.waitForResponse(r=>r.url().endsWith('/api/reports')&&r.request().method()==='POST')
  await form(page).getByRole('button',{name:'Отправить',exact:true}).click()
  const response=await result;expect(response.status()).toBe(200)
  expect(response.request().postDataJSON()).toMatchObject({kind:'suggestion',screenshot:null})
  await expect(page.getByRole('dialog',{name:'Отчёт сохранён'})).toBeVisible()
})

test('capture failure leaves a usable cancellable form without uploading anything',async({page})=>{
  test.slow();await village(page)
  await page.route('**/*html2canvas*',route=>route.abort('failed'))
  const writes:string[]=[]
  page.on('request',r=>{if(r.url().includes('/api/reports'))writes.push(r.method())})
  await trigger(page).click();await expect(form(page)).toBeVisible()
  await expect(form(page).getByText('Без снимка экрана.',{exact:false})).toBeVisible()
  await form(page).getByRole('button',{name:'Отмена',exact:true}).click()
  expect(writes).toEqual([])
})

test('report control also covers the actual dungeon page and records its run ID',async({page})=>{
  test.slow();const slug=await village(page)
  const runId='12345678-1234-1234-1234-123456789abc'
  await page.goto(`/v/${slug}/dungeon/${runId}`)
  await expect(page.locator('.dungeon-root .game-canvas canvas')).toBeVisible()
  await trigger(page).click();await expect(form(page).locator('img')).toBeVisible()
  await form(page).getByLabel('Описание').fill('Dungeon report fixture')
  let body:Record<string,unknown>|null=null
  await page.route('**/api/reports',async route=>{
    body=route.request().postDataJSON()
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({id:body!.id,state:'saved'})})
  })
  await form(page).getByRole('button',{name:'Отправить',exact:true}).click()
  await expect(page.getByRole('dialog',{name:'Отчёт сохранён'})).toBeVisible()
  expect(body).toMatchObject({slug,screen:{page:'dungeon',dungeonId:runId}})
})
