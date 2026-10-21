import {test,expect,uniqueName} from './fixtures.ts'

for(const language of ['ru','en'])test(`ruler sees and withdraws the existing resident fighter with its real health (${language})`,async({page})=>{
 await page.setViewportSize({width:320,height:760})
 await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
 await page.request.post('/auth/dev-login',{data:{name:uniqueName('actor-ui')}})
 const commands:{mode:string}[]=[]
 await page.route('**/api/villages/stand/battles/raid/order',async route=>{commands.push(route.request().postDataJSON());await route.fulfill({status:200,body:''})})
 await page.goto('/e2e/post-071-harness.html?legacyFighter=1')
 await expect(page.locator('.battle-controls')).toContainText(language==='ru'?'Моё здоровье: 7/32':'My health: 7/32')
 await expect.poll(()=>commands.length).toBe(1)
 const panel=page.getByRole('group',{name:language==='ru'?'Действия в бою':'Combat actions'})
 await panel.getByRole('button',{name:language==='ru'?'Лечение':'Heal',exact:true}).click()
 await panel.getByRole('button',{name:language==='ru'?'Отступить':'Retreat',exact:true}).click()
 await expect.poll(()=>commands.map(c=>c.mode)).toEqual(['intercept','heal','retreat'])
 await expect(page.locator('.battle-controls')).toContainText('7/32')
 for(const button of await panel.getByRole('button').all()){
  const box=(await button.boundingBox())!;expect(box.height).toBeGreaterThanOrEqual(44);expect(box.x).toBeGreaterThanOrEqual(0);expect(box.x+box.width).toBeLessThanOrEqual(320)
 }
})
