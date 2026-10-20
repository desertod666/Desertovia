import {test,expect} from './fixtures.ts'
import './regional-contract.ts'
import vectors from './fixtures/regional-terrain.json' with {type:'json'}

for(const vector of vectors)test(`southern terrain preserves the original map and agrees with server geometry (${vector.seed})`,async({page})=>{
 await page.goto('/e2e/world-harness.html?regions=1')
 const measured=await page.evaluate(seed=>window.inspectRegions(seed),vector.seed)
 expect(measured.legacySize).toBe(10800);expect(measured.size).toBe(18000)
 expect(measured.extraHash).toBe(vector.extraHash)
 if(vector.seed===0)expect(measured.legacyMaskHash).toBe('d0f40db3e180d5a9f36346e90e5d178cd1db551ecd93a793477738ce86e8a830')
 expect(measured.reachable).toBe(true);expect(measured.waterBlocked).toBe(true)
 expect(measured.marshTiles).toBeGreaterThan(100)
 for(const kind of ['1','3','4','5','14'])expect(measured.extraCounts[kind]).toBeGreaterThan(0)
 await test.info().attach('map-budget',{body:JSON.stringify(measured,null,2),contentType:'application/json'})
})

for(const language of ['ru','en'])test(`known southern trails require an explicit walk and respect bridge access (${language})`,async({page},info)=>{
 await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
 await page.setViewportSize({width:info.project.name==='phone'?320:960,height:760})
 await page.goto('/e2e/world-harness.html?regions=1')
 const atlas=page.getByRole('dialog',{name:language==='ru'?'Округа':'District atlas',exact:true})
 await expect(atlas.locator('.world-route-list button')).toHaveCount(7)
 const select=atlas.getByRole('combobox',{name:language==='ru'?'Место':'Destination',exact:true})
 if(await select.isVisible())await select.selectOption('southern-quarry');else await atlas.getByRole('button',{name:language==='ru'?/Южный каменный кряж/:/Southern quarry/}).click()
 const walk=atlas.getByRole('button',{name:language==='ru'?'Идти по тропе':'Walk the trail',exact:true})
 await expect(walk).toBeDisabled()
 if(await select.isVisible())await select.selectOption('southern-marsh');else await atlas.getByRole('button',{name:language==='ru'?/Южные болота/:/Southern marsh/}).click()
 await expect(walk).toBeEnabled();await expect(atlas.getByRole('button',{name:language==='ru'?'Показать на карте':'Show on map'})).toBeDisabled()
 expect(await page.evaluate(()=>window.atlasTravel)).toEqual([])
 expect(await atlas.evaluate(el=>el.scrollWidth<=el.clientWidth)).toBe(true)
 await page.screenshot({path:test.info().outputPath(`southern-atlas-${language}.png`)})
 await walk.click();await expect(atlas).toHaveCount(0)
 expect(await page.evaluate(()=>window.atlasTravel)).toEqual([{x:14.5,y:92.5}])
 expect(await page.evaluate(()=>window.atlasFocus)).toEqual([])
})
