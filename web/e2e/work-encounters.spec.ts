import {test,expect} from './fixtures.ts'

for(const language of ['ru','en'])test(`a personal encounter identifies its enemy and reports only its actual work settlement (${language})`,async({page},info)=>{
 await page.addInitScript(value=>localStorage.setItem('desertod.language',value),language)
 await page.setViewportSize({width:info.project.name==='phone'?320:1280,height:760})
 await page.goto('/e2e/world-harness.html?retreat=1&encounter=1')
 await expect(page.locator('.dungeon-top strong')).toContainText(language==='ru'?'Засада':'Ambush')
 await expect(page.locator('.dungeon-top strong')).toContainText(language==='ru'?'Волк':'Wolf')
 await page.goto('/e2e/world-harness.html?retreat=1&encounter=1&ended=1')
 const result=page.getByRole('dialog',{name:language==='ru'?'Итог засады':'Encounter result'})
 await expect(result).toContainText(language==='ru'?'учтён в результате задания':'included in your work result')
 await expect(result.getByRole('link',{name:language==='ru'?'Вернуться в мир':'Return to the world'})).toHaveAttribute('href','/v/test')
 expect(await result.evaluate(el=>el.scrollWidth<=el.clientWidth)).toBe(true)
 await page.goto('/e2e/world-harness.html?retreat=1&encounter=1&ended=1&pending=1')
 await expect(result).toContainText(language==='ru'?'ожидает завершения восстановления':'waiting for recovery')
 await expect(result).not.toContainText(language==='ru'?'учтён в результате задания':'included in your work result')
})
