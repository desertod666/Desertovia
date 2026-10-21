import { readFileSync } from 'node:fs'
import { createHash } from 'node:crypto'
import { test, expect } from './fixtures.ts'
type RaidBattle={revision:number;phase?:string;finishedAt?:string|null;fighters?:{citizenId:string;health:number;active:boolean;mode?:string;isTsar?:boolean}[];units:{id:string;x:number;y:number;health:number;state:string;diedAt:string|null}[];[key:string]:unknown}
type Citizen={energy:number;maximumEnergy:number;energyPerHour:number;serverNow:string}

type Stand={terrain:(seed:number,wilds:unknown)=>number[];initial:RaidBattle;orders:{target:string|null;mode:string}[];update:(b:RaidBattle,time?:number)=>{children:number;positions:number[][];rotations:number[]};rendered:(id:string)=>{x:number;y:number};pick:(x:number,y:number)=>unknown;mergeBattles:(a:RaidBattle[],b:RaidBattle[])=>RaidBattle[];adoptVillageState:(previous:{id:string;battles:RaidBattle[];fortifications?:{damage:number}[]},next:{id:string;snapshotAt?:string;battles:RaidBattle[];fortifications?:{damage:number}[]})=>{battles:RaidBattle[];fortifications?:{damage:number}[]};fightingCitizenIds:(battles:RaidBattle[])=>string[];currentEnergy:(c:Citizen,now:number)=>number}

test('live renderer shows every actual enemy, individual deaths and authoritative positions',async({page})=>{
  await page.goto('/e2e/live-combat-harness.html');await page.waitForFunction(()=>!!(window as unknown as {combatStand:unknown}).combatStand)
  const result=await page.evaluate(()=>{
    const s=(window as unknown as {combatStand:Stand}).combatStand
    const b=structuredClone(s.initial)
    const start=s.update(b)
    b.units[0].health=0;b.units[0].state='Dead';b.units[0].diedAt='2026-10-15T12:00:02Z'
    b.units[1].x+=1;b.revision++
    const changed=s.update(b)
    const drawn=s.rendered('enemy-1')
    return {count:start.children,changed:changed.children,dead:changed.rotations.filter(n=>Math.abs(n-Math.PI/2)<.01).length,
      picked:s.pick(drawn.x,drawn.y),headPick:s.pick(drawn.x,drawn.y-24),positionsChanged:JSON.stringify(start.positions)!==JSON.stringify(changed.positions)}
  })
  expect(result.count).toBe(26) // 12 enemy actor/bar pairs and the guard health bar pair.
  expect(result.changed).toBe(26);expect(result.dead).toBe(1);expect(result.positionsChanged).toBe(true)
  expect(result.picked).toEqual({battleId:'battle',unitId:'enemy-1'});expect(result.headPick).toEqual(result.picked)
})

for(const width of [320,390,1280])test(`enemy controls fit ${width}px and send orders without local damage`,async({page})=>{
  await page.setViewportSize({width,height:844})
  await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
  await page.goto('/e2e/live-combat-harness.html')
  const card=page.getByRole('complementary',{name:'Выбранный противник'})
  await expect(card).toContainText('Моё здоровье: 26/40')
  await expect(card).toContainText('8')
  await expect(card.getByRole('status')).toContainText('Цель выбрана')
  await expect(card.getByRole('button',{name:/Атаковать|Ближайший враг|Держать позицию|Перехватывать/i})).toHaveCount(0)
  await page.getByRole('button',{name:'Обновить здоровье'}).click()
  expect(await page.evaluate(()=> (window as unknown as {combatStand:Stand}).combatStand.orders)).toEqual([{target:'enemy-0',mode:'intercept'}])
  await page.getByRole('button',{name:'Выбрать другого врага'}).click()
  await expect.poll(()=>page.evaluate(()=> (window as unknown as {combatStand:Stand}).combatStand.orders)).toEqual([{target:'enemy-0',mode:'intercept'},{target:'enemy-1',mode:'intercept'}])
  await page.getByRole('button',{name:'Открыть или закрыть меню'}).click()
  await expect(card).toHaveCount(0)
  await page.getByRole('button',{name:'Открыть или закрыть меню'}).click()
  await expect(card).toBeVisible()
  expect(await page.evaluate(()=> (window as unknown as {combatStand:Stand}).combatStand.orders)).toHaveLength(2)
  await page.getByRole('button',{name:'Повторно выбрать врага'}).click()
  await expect.poll(()=>page.evaluate(()=> (window as unknown as {combatStand:Stand}).combatStand.orders.length)).toBe(3)
  await expect(card.locator('progress')).toHaveAttribute('value','20')
  expect(await card.evaluate(el=>el.scrollWidth<=el.clientWidth)).toBe(true)
  const box=await card.boundingBox();expect(box!.x).toBeGreaterThanOrEqual(0);expect(box!.x+box!.width).toBeLessThanOrEqual(width)
  await page.screenshot({path:test.info().outputPath(`combat-${width}.png`)})
})

test('steady combat animation reuses health geometry until health actually changes',async({page})=>{
  await page.goto('/e2e/live-combat-harness.html')
  await page.waitForFunction(()=>!!(window as unknown as {combatStand:unknown}).combatStand)
  const result=await page.evaluate(()=>(window as unknown as {combatStand:{frames:()=>{stableUpdates:number;changedUpdates:number;elapsed:number}}}).combatStand.frames())
  expect(result.stableUpdates).toBe(0)
  expect(result.changedUpdates).toBeGreaterThan(0)
  test.info().annotations.push({type:'animation-cpu-ms-300-frames',description:String(result.elapsed)})
})

test('a stale village snapshot cannot rewind a newer battle revision and energy projects fractional recovery',async({page})=>{
  await page.goto('/e2e/live-combat-harness.html');await page.waitForFunction(()=>!!(window as unknown as {combatStand:unknown}).combatStand)
  const result=await page.evaluate(()=>{
    const s=(window as unknown as {combatStand:Stand}).combatStand
    const fresh=structuredClone(s.initial);fresh.revision=4;fresh.units[0].health=7;fresh.lastTickAt='2026-10-15T12:00:05Z'
    const result=s.mergeBattles([fresh],[s.initial])[0]
    const ended=structuredClone(s.initial);ended.id='old';ended.revision=9
    const adopted=s.adoptVillageState(
      {id:'test',battles:[fresh,ended],fortifications:[{damage:3}]},
      {id:'test',battles:[structuredClone(s.initial)],fortifications:[{damage:1}]})
    const c={energy:.5,maximumEnergy:40,energyPerHour:4,serverNow:'2026-10-15T12:00:00Z'} as Citizen
    return {revision:result.revision,hp:result.units[0].health,energy:s.currentEnergy(c,Date.parse(c.serverNow)+900000),
      kept:adopted.battles.map(b=>b.id),keptRevision:adopted.battles[0].revision,keptHp:adopted.battles[0].units[0].health,wall:adopted.fortifications?.[0].damage}
  })
  expect(result).toEqual({revision:4,hp:7,energy:1.5,kept:['battle'],keptRevision:4,keptHp:7,wall:3})
})

test('an away fighter stays listed so the green health bar keeps a body',async({page})=>{
  await page.goto('/e2e/live-combat-harness.html');await page.waitForFunction(()=>!!(window as unknown as {combatStand:unknown}).combatStand)
  const ids=await page.evaluate(()=>{
    const s=(window as unknown as {combatStand:Stand}).combatStand
    const live=structuredClone(s.initial)
    const fighter=live.fighters![0]
    const warning=structuredClone(s.initial);warning.phase='Warning';warning.fighters=[{...fighter,citizenId:'waiting'}]
    const gone=structuredClone(s.initial);gone.fighters=[{...fighter,citizenId:'down',health:0,active:false}]
    return [...s.fightingCitizenIds([live,warning,gone])]
  })
  expect(ids).toEqual(['me'])
})


test('server collision fixtures still match the authored ponds, mountains and marsh',async({page})=>{
  await page.goto('/e2e/live-combat-harness.html');await page.waitForFunction(()=>!!(window as unknown as {combatStand:unknown}).combatStand)
  const cases=JSON.parse(readFileSync(new URL('./fixtures/authored-terrain.json',import.meta.url),'utf8')) as {seed:number;wilds:unknown;hash:string}[]
  for(const sample of cases){
    const mask=await page.evaluate(({seed,wilds})=>(window as unknown as {combatStand:Stand}).combatStand.terrain(seed,wilds),sample)
    expect(mask).toHaveLength(18000)
    // Historical golden hashes cover every original tile. The southern extension has independent full-tile vectors.
    expect(createHash('sha256').update(Buffer.from(mask.slice(0,10800))).digest('hex')).toBe(sample.hash)
  }
})


test('the active second wave stays in the banner ahead of an overdue queued third wave',async({page})=>{
  await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
  await page.goto('/e2e/live-combat-harness.html')
  await expect(page.locator('#queue-banner')).toContainText('в деревне')
  await expect(page.locator('#queue-banner')).toContainText('волна 2 из 3')
})


test('health-only and stale packets preserve the movement timeline while discontinuities snap',async({page})=>{
  await page.goto('/e2e/live-combat-harness.html');await page.waitForFunction(()=>!!(window as unknown as {combatStand:unknown}).combatStand)
  const result=await page.evaluate(()=>{
    type Sample={battle:string;revision:number;x:number;y:number;fromX:number;fromY:number;at:number}
    const s=(window as unknown as {combatStand:{receiveFighterSample:(old:Sample|undefined,battle:string,rev:number,x:number,y:number,at:number)=>Sample;fighterPositionAt:(sample:Sample,at:number)=>{x:number;y:number}}}).combatStand
    const initial=s.receiveFighterSample(undefined,'battle',1,10,10,0)
    const moved=s.receiveFighterSample(initial,'battle',2,11,10,1000)
    const half=s.fighterPositionAt(moved,1500)
    const health=s.receiveFighterSample(moved,'battle',3,11,10,1500)
    const stale=s.receiveFighterSample(health,'battle',2,10,10,1600)
    const jump=s.receiveFighterSample(stale,'battle',4,20,20,1700)
    return {half,afterHealth:s.fighterPositionAt(health,1500),afterStale:s.fighterPositionAt(stale,1750),end:s.fighterPositionAt(stale,3000),jump:s.fighterPositionAt(jump,1700)}
  })
  expect(result.afterHealth).toEqual(result.half)
  expect(result.afterStale.x).toBeGreaterThan(result.half.x);expect(result.afterStale.x).toBeLessThan(11)
  expect(result.end).toEqual({x:11,y:10});expect(result.jump).toEqual({x:20,y:20})
})


test('boss warning is fixed to the impact point and disappears after the authoritative strike',async({page})=>{
  await page.goto('/e2e/live-combat-harness.html');await page.waitForFunction(()=>!!(window as unknown as {combatStand:unknown}).combatStand)
  const result=await page.evaluate(()=>{
    type Boss={variant:string;strikeAt:string|null;strikeX:number;strikeY:number}
    type Geometry={scale:number;warning:{x:number;y:number;width:number}|null}
    const s=(window as unknown as {combatStand:Stand&{geometry:(id:string)=>Geometry}}).combatStand
    const b=structuredClone(s.initial),u=b.units[0] as typeof b.units[number]&Boss
    u.variant='boss';u.strikeAt='2026-10-15T12:00:05Z';u.strikeX=4;u.strikeY=3;u.state='Windup';b.revision++
    s.update(b);const before=s.geometry(u.id)
    u.x+=.5;b.revision++;s.update(b);const during=s.geometry(u.id)
    u.strikeAt=null;u.state='Raiding';b.revision++;s.update(b)
    return {before,during,after:s.geometry(u.id)}
  })
  expect(result.before.scale).toBeCloseTo(1.7)
  expect(result.before.warning).toEqual(result.during.warning)
  expect(result.before.warning?.x).toBe(64);expect(result.before.warning?.y).toBe(48)
  expect(result.before.warning!.width).toBeGreaterThan(50)
  expect(result.after.warning).toBeNull()
})


test('an older full snapshot cannot erase a battle that started later, and a new village never inherits it', async ({page}) => {
  await page.goto('/e2e/live-combat-harness.html')
  await page.waitForFunction(() => !!(window as unknown as {combatStand:unknown}).combatStand)
  const result = await page.evaluate(() => {
    const s = (window as unknown as {combatStand:Stand}).combatStand
    const battle = structuredClone(s.initial)
    battle.lastTickAt = '2026-10-18T10:00:05Z'
    const before = {id:'v',battles:[battle]}
    const old = s.adoptVillageState(before,{id:'v',battles:[],snapshotAt:'2026-10-18T10:00:01Z'})
    const fresh = s.adoptVillageState(before,{id:'v',battles:[],snapshotAt:'2026-10-18T10:00:06Z'})
    const other = s.adoptVillageState(before,{id:'other',battles:[]})
    return [old.battles.length,fresh.battles.length,other.battles.length]
  })
  expect(result).toEqual([1,0,0])
})
