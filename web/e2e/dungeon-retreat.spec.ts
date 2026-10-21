import {test,expect} from './fixtures.ts'

for(const language of ['ru','en'])test(`the player can request one visible retreat and read the vulnerability hint (${language})`,async({page},info)=>{
 await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
 await page.setViewportSize({width:info.project.name==='phone'?320:1280,height:760})
 await page.goto('/e2e/world-harness.html?retreat=1')
 const button=page.getByRole('button',{name:language==='ru'?'Отступить':'Retreat',exact:true})
 await expect(button).toBeEnabled();const box=(await button.boundingBox())!;expect(box.height).toBeGreaterThanOrEqual(44);expect(box.x).toBeGreaterThanOrEqual(0);expect(box.x+box.width).toBeLessThanOrEqual(info.project.name==='phone'?320:1280)
 await page.getByRole('button',{name:language==='ru'?'Об отступлении':'About retreat'}).click()
 await expect(page.getByRole('tooltip')).toContainText(language==='ru'?'остаётся уязвимым':'remains vulnerable')
 await page.keyboard.press('Escape');await button.click()
 await expect(page.locator('#retreat-count')).toHaveText('1')
 await expect(page.getByRole('button',{name:language==='ru'?'Отступаю…':'Withdrawing…',exact:true})).toBeDisabled()
 await expect(page.locator('.dungeon-hint')).toContainText(language==='ru'?'к выходу':'to the exit')
 await page.goto('/e2e/world-harness.html?retreat=1&left=1')
 await expect(page.locator('.dungeon-hint')).toContainText(language==='ru'?'Вы вышли':'You are out')
 await expect(page.locator('.dungeon-retreat')).toHaveCount(0)
})

test('a spectator sees no retreat control for another hero',async({page})=>{
 await page.goto('/e2e/world-harness.html?retreat=1&watching=1')
 await expect(page.locator('.dungeon-top')).toBeVisible();await expect(page.locator('.dungeon-retreat')).toHaveCount(0)
 await expect(page.locator('#retreat-count')).toHaveText('0')
})
