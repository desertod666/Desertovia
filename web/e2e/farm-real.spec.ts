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

test('real resting resident inspects the map farm without waking or accepting work',async({page},info)=>{
 await page.addInitScript(()=>localStorage.setItem('desertod.language','en'))
 await page.setViewportSize({width:info.project.name==='phone'?320:960,height:760})
 await page.goto('/v/demo-built');await developmentLogin(page,uniqueName('farm-rest-map'));await expectVillageView(page)
 await page.locator('.safe-rest-control button').first().click()
 await expect(page.locator('.safe-rest-control')).toHaveAttribute('data-state','Resting')
 const before=await page.request.get('/api/villages/demo-built/me').then(r=>r.json())
 const world=await page.request.get('/api/villages/demo-built').then(r=>r.json())
 const farm=world.state.structures.find((s:{kind:string})=>s.kind==='Farm')
 const xy={x:Number(farm.plotId.split(',')[0]),y:Number(farm.plotId.split(',')[1])}
 const writes:string[]=[]
 page.on('request',request=>{if(request.method()!=='GET'&&request.url().includes('/api/villages/'))writes.push(request.url())})
 const minimap=page.locator('.minimap');await expect(minimap).toBeVisible()
 const bounds=(await minimap.locator('canvas').first().boundingBox())!
 await page.mouse.click(bounds.x+(xy.x+1.5+44)/120*bounds.width,bounds.y+(xy.y+1.5+32)/90*bounds.height)
 const canvas=page.locator('.game-canvas>canvas');const map=(await canvas.boundingBox())!
 await canvas.click({position:{x:map.width/2,y:map.height/2}})
 const popup=page.locator('.farm-popup');await expect(popup).toBeVisible()
 await expect(popup.getByRole('toolbar')).toHaveCount(0)
 await popup.getByRole('button',{name:'Bed 1',exact:true}).click()
 await expect(popup.locator('.farm-selection')).toBeInViewport()
 await expect(page.locator('.safe-rest-control')).toHaveAttribute('data-state','Resting')
 const after=await page.request.get('/api/villages/demo-built/me').then(r=>r.json())
 expect(after.restState).toBe('Resting');expect(after.cycleStartedAt).toBe(before.cycleStartedAt)
 expect(writes).toEqual([])
 await page.screenshot({path:test.info().outputPath('farm-resting-inspect.png')})
})
