import {test,expect,uniqueName} from './fixtures.ts'

for(const language of ['ru','en'])test(`ordinary royal dungeon class stays usable on phone and survives a failed acknowledgement (${language})`,async({page})=>{
 await page.setViewportSize({width:320,height:760})
 await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
 await page.request.post('/auth/dev-login',{data:{name:uniqueName('royal-class')}})
 const requests:unknown[]=[]
 await page.route('**/api/villages/stand/tsar/dungeon',async route=>{
  if(route.request().method()==='GET'){
   await route.fulfill({json:[{class:'Brawler',health:20,damage:1,range:.9,weapon:null,armor:null},{class:'Archer',health:18,damage:6,range:5,weapon:'Bow',armor:null}]});return
  }
  requests.push(route.request().postDataJSON())
  if(requests.length===1){await route.fulfill({status:503,json:{code:'test.retry',detail:'Try again'}});return}
  const next=await page.evaluate(()=>structuredClone((window as unknown as {royalDungeonStand:{initial:{dungeon:{heroes:{combatClass:string}[]}}}}).royalDungeonStand.initial))
  next.dungeon.heroes[0].combatClass='Archer';await route.fulfill({json:next})
 })
 await page.goto('/e2e/post-071-harness.html?royalDungeon=1')
 const picker=page.locator('.royal-dungeon-class'),select=picker.getByRole('combobox')
 await expect(select).toBeEnabled();await select.selectOption('Archer');await expect(picker).toContainText('18')
 const button=picker.getByRole('button',{name:language==='ru'?'Идти в бой':'Join the fight',exact:true})
 for(const control of [select,button]){
  const box=(await control.boundingBox())!;expect(box.height).toBeGreaterThanOrEqual(44);expect(box.x).toBeGreaterThanOrEqual(0);expect(box.x+box.width).toBeLessThanOrEqual(320)
 }
 await button.click();await expect(picker.getByRole('alert')).toBeVisible();await expect(select).toHaveValue('Archer')
 await button.click();await expect(button).toBeDisabled();await expect(picker.getByRole('alert')).toHaveCount(0)
 expect(requests).toEqual([{class:'Archer'},{class:'Archer'}])
 expect(await picker.evaluate(el=>el.scrollWidth<=el.clientWidth)).toBe(true)
 await page.screenshot({path:test.info().outputPath(`royal-dungeon-class-${language}-320.png`)})
})
