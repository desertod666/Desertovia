import {test,expect} from './fixtures.ts'
import './journey-contract.ts'

for(const language of ['ru','en'])test(`a blocked journey waits and retries an uncertain reply without a new command (${language})`,async({page},info)=>{
 await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
 await page.setViewportSize({width:info.project.name==='phone'?320:1280,height:760})
 await page.goto('/e2e/world-harness.html?journey=1&future=1')
 const card=page.getByRole('region',{name:language==='ru'?'Дорога героя':'Hero journey'})
 const go=card.getByRole('button',{name:language==='ru'?'Продолжить':'Continue journey',exact:true})
 await expect(go).toBeDisabled();expect(await page.evaluate(()=>window.journeyRequests)).toEqual([])
 await page.getByRole('button',{name:'Reach obstruction'}).click();await expect(go).toBeEnabled()
 expect(await card.evaluate(el=>el.getBoundingClientRect().left>=0&&el.getBoundingClientRect().right<=innerWidth)).toBe(true)
 expect((await go.boundingBox())!.height).toBeGreaterThanOrEqual(44)
 await card.getByRole('button',{name:language==='ru'?'О перекрытой дороге':'About the blocked route'}).click()
 await expect(page.getByRole('tooltip')).toBeVisible();await page.keyboard.press('Escape');await expect(page.getByRole('tooltip')).toHaveCount(0)
 const atlas=card.getByRole('button',{name:language==='ru'?'Округа':'District atlas',exact:true})
 expect(Math.abs((await go.boundingBox())!.y-(await atlas.boundingBox())!.y)).toBeLessThan(1)
 await atlas.click()
 expect(await page.evaluate(()=>window.journeyAtlasOpened)).toBe(true);expect(await page.evaluate(()=>window.journeyRequests)).toEqual([])
 await go.click();await expect(card.getByRole('alert')).toBeVisible();await expect(go).toBeEnabled()
 await go.click();await expect(card).toHaveCount(0)
 const sent=await page.evaluate(()=>window.journeyRequests);expect(sent).toHaveLength(2);expect(sent[0]).toEqual(sent[1]);expect(sent[0].requestId).toMatch(/^[\da-f-]{36}$/i)
})

test('the ordinary cancellation is explicit and never continues the route',async({page})=>{
 await page.addInitScript(()=>localStorage.setItem('desertod.language','en'))
 await page.goto('/e2e/world-harness.html?journey=1')
 await page.getByRole('button',{name:'Cancel work',exact:true}).click()
 await expect(page.locator('.journey-control')).toHaveCount(0)
 expect(await page.evaluate(()=>window.journeyCancelled)).toBe(true)
 expect(await page.evaluate(()=>window.journeyRequests)).toEqual([])
})
