import {test,expect,developmentLogin,expectVillageView,openMenu,uniqueName} from './fixtures.ts'

test.setTimeout(120_000)
test.use({deviceScaleFactor:1,trace:{mode:'retain-on-failure',screenshots:false,snapshots:true}})

test('real task menu opens the farm and requires a chosen bed before submitting work',async({page},info)=>{
 await page.addInitScript(()=>localStorage.setItem('desertod.language','en'))
 await page.setViewportSize({width:info.project.name==='phone'?320:960,height:760})
 await page.goto('/v/demo-built');await developmentLogin(page,uniqueName('farm-map'));await expectVillageView(page)
 await expect.poll(async()=>(await page.request.get('/api/villages/demo-built/me')).status()).toBe(200)
 const me=await page.request.get('/api/villages/demo-built/me').then(r=>r.json())
 const world=await page.request.get('/api/villages/demo-built').then(r=>r.json())
 const bed=world.state.fields.find((b:{growth:number})=>b.growth>=1)
 expect(bed).toBeTruthy()
 const calls:Record<string,unknown>[]=[]
 await page.route('**/api/villages/demo-built/me/task',route=>{calls.push(route.request().postDataJSON());return route.fulfill({json:me})})
 const menu=await openMenu(page);await menu.getByRole('button',{name:'Tasks',exact:true}).click()
 const harvest=menu.getByRole('button',{name:/^Harvest\b/});await expect(harvest).toBeEnabled();await harvest.click()
 const popup=page.locator('.farm-popup');await expect(popup).toBeVisible()
 expect(calls).toHaveLength(0)
 await expect(popup.getByRole('toolbar').getByRole('button',{name:'Harvest',exact:true})).toHaveAttribute('aria-pressed','true')
 await popup.getByRole('button',{name:`Bed ${bed.index+1}`,exact:true}).click()
 await expect.poll(()=>calls).toHaveLength(1)
 expect(calls[0]).toMatchObject({taskId:'harvest',workplacePlotId:bed.plotId,farmTarget:{plotId:bed.plotId,index:bed.index,generationId:bed.generationId}})
 expect(calls[0].requestId).toMatch(/^[0-9a-f-]{36}$/i)
})
