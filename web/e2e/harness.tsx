// Test-only entry point; excluded from normal production builds.
import { VillageWorld } from '../src/game/VillageWorld'
import { HutConstruction, siteVisualKey } from '../src/game/HutConstruction'
import { Application, Container, Graphics, Text } from 'pixi.js'
import { createRoot } from 'react-dom/client'
import { useState } from 'react'
import { BuildingLighting } from '../src/game/BuildingLighting'
import { TileSet } from '../src/game/sheets'
import registrations from '../src/assets/night/registration.json'
import { StorageCard } from '../src/ui/StorageCard'
import { BottomBar } from '../src/ui/Hud'
import { ChatPanel } from '../src/ui/ChatPanel'
import { I18nProvider } from '../src/i18n'
import type { Catalog, Citizen, Site, VillageState } from '../src/api'
import '../src/index.css'

const app=new Application(); await app.init({width:650,height:700,backgroundAlpha:0,resolution:1,preference:'webgl'})
document.getElementById('lighting')!.appendChild(app.canvas)
const tiles=await TileSet.load(); const root=new Container();app.stage.addChild(root)
const views=Object.keys(registrations).map((kind,i)=>{
 const view=new BuildingLighting(tiles,kind,(i%4)*7,Math.floor(i/4)*6);root.addChild(view,view.light);return view
})
Object.assign(window,{lightingTest:{
 measure:()=>views.map(v=>{const b=v.getLocalBounds();return {x:v.x,y:v.y,bounds:[b.minX,b.minY,b.maxX,b.maxY]}}),
 render:(amount:number)=>{views.forEach(v=>v.setNight(amount));app.renderer.render(app.stage)},
 emissionCheck:()=>views.map(view=>{
  // Pixi extraction normalizes the target's own alpha. Keep the light as a child, as in the real world.
  const parent=new Container();parent.addChild(view.light)
  view.setNight(0);const day=app.renderer.extract.pixels({target:parent,resolution:2})
  view.setNight(1);const night=app.renderer.extract.pixels({target:parent,resolution:2})
  root.addChild(view.light);parent.destroy()
  return {day:day.pixels.reduce((sum,value,i)=>sum+(i%4===3?value:0),0),night:night.pixels.reduce((sum,value,i)=>sum+(i%4===3?value:0),0)}
 }),
 outlineCheck:()=>Object.values(registrations).map(pair=>[pair.day,pair.night].map(name=>{
  // Baked ink lives in the ready texture; map night compensation is a separate lighting pass.
  const image=app.renderer.extract.pixels({target:tiles.nightBuilding(name)});let ink=0
  for(let i=0;i<image.pixels.length;i+=4){const p=image.pixels;if(p[i+3]===255&&Math.abs(p[i]-43)<=1&&Math.abs(p[i+1]-29)<=1&&Math.abs(p[i+2]-43)<=1)ink++}
  return ink
 })),
 alphaCheck:()=>views.map(view=>{
  const samples=[0,0.5,1].map(level=>{view.setNight(level);return app.renderer.extract.pixels({target:view,resolution:3})})
  const [day,middle,night]=samples;let common=0,transparent=0
  for(let i=3;i<day.pixels.length;i+=4) if(day.pixels[i]>250&&night.pixels[i]>250){common++;if(middle.pixels[i]<250)transparent++}
  return {common,transparent,dimensions:samples.map(s=>[s.width,s.height])}
 }),
}})
const catalog=await fetch('/api/catalog').then(r=>r.json()) as Catalog
const result=await fetch('/api/villages/demo-bridge').then(r=>r.json())
// Get a realistic DTO for UI checks without changing a player or world.
const originalCitizen=await fetch('/api/villages/demo-bridge/me').then(r=>r.json())
const citizen={...originalCitizen, load:[], state:'AwaitingRenewal', taskId:'hunt-hare',
 cycleStartedAt:new Date(Date.now()-60000).toISOString(),cycleEndsAt:new Date(Date.now()-1000).toISOString(),
 graceEndsAt:new Date(Date.now()+55000).toISOString()} as Citizen
const uiNow=Date.now()
function Controls(){
 const [stock,setStock]=useState(7),[taken,setTaken]=useState(0)
 return <I18nProvider>
  <div className="hud" style={{position:'relative',height:'auto'}}>
   <BottomBar citizen={citizen} catalog={catalog} structures={result.state.structures} now={uiNow}
    phone tsar={false} ruler hasResult pending={false} onMenu={()=>{}} onInventory={()=>{}} onShowResult={()=>{}}
    poll={{label:'Начать голосование',onClick:()=>{}}} onCancelTask={()=>{}} drinks={0} onDrink={()=>{}} remedies={0} onHeal={()=>{}} />
  </div>
  <StorageCard storage={{kind:'Heap',plotId:'0,0',damage:0,slots:[],goods:[{resource:'Food',amount:stock}]}} catalog={catalog}
   take={{citizen,disabled:false,onTake:(_,amount)=>setTaken(amount)}}/>
  <button id="stock-update" onClick={()=>setStock(stock+1)}>Refresh stock</button><output id="taken">{taken}</output>
  <ChatPanel feed={[]} messages={[{id:'npc',authorName:'Отец Срамослав',citizenId:null,isTsar:false,isChaplain:true,text:'Держимся вместе, чада!',sentAt:new Date(uiNow+1000).toISOString()}]}
   member tsar ownCitizenId={null} serverOffsetMs={0} phone={false} onSend={async()=>{}} />
 </I18nProvider>
}
createRoot(document.getElementById('app')!).render(<Controls/>);

// Production construction class, rendered with the real catalog and map darkness layering.
const hutDefinition=catalog.buildings.find(building=>building.kind==='Hut')!
const constructionApp=new Application();await constructionApp.init({width:640,height:350,background:0x83b765,resolution:1,preference:'webgl'})
constructionApp.canvas.id='construction-preview';document.body.appendChild(constructionApp.canvas)
const constructionRoot=new Container();constructionRoot.scale.set(2);constructionApp.stage.addChild(constructionRoot)
const dark=new Graphics().rect(0,0,320,175).fill({color:0x0b1640,alpha:1});dark.alpha=0
const constructionLights=new Container()
const sampleSite=(stage:number):Site=>({kind:'Hut',plotId:'8,8',stage,progress:stage===5?0.94:stage/6,
 stages:hutDefinition.stages.map(def=>({id:def.id,crewNeeded:def.crew,hands:1,
 jobs:def.jobs.map(job=>({jobId:job.jobId,required:job.amount,done:job.amount,room:0}))}))})
const constructionViews=hutDefinition.stages.map((_,i)=>{
 const view=new HutConstruction(tiles,sampleSite(i),hutDefinition,2+(i%4)*5,2+Math.floor(i/4)*5)
 constructionRoot.addChild(view);constructionLights.addChild(view.light)
 const label=new Text({text:['Подготовка','Основание','Каркас','Стены','Крыша','Отделка'][i],style:{fontSize:6,fill:0x201b10}})
 label.position.set(view.x,view.y-8);constructionRoot.addChild(label)
 return view
})
const completedHut=new BuildingLighting(tiles,'Hut',12,7);constructionRoot.addChild(completedHut);constructionLights.addChild(completedHut.light)
const completeLabel=new Text({text:'Готово',style:{fontSize:6,fill:0x201b10}});completeLabel.position.set(192,104);constructionRoot.addChild(completeLabel)
constructionRoot.addChild(dark,constructionLights)
Object.assign(window,{constructionTest:{
 render:(night:number)=>{dark.alpha=night*0.55;constructionViews.forEach(view=>view.setNight(night));completedHut.setNight(night);constructionApp.renderer.render(constructionApp.stage)},
 phases:()=>constructionViews.map(view=>view.phase),
 coordinates:()=>constructionViews.map(view=>({position:[view.x,view.y],light:[view.light.x,view.light.y]})),
 delivery:()=>{const before=sampleSite(2);before.stages[2].jobs[0].done=0;const key=JSON.stringify(siteVisualKey(before));before.stages[2].jobs[0].done=1;return key!==JSON.stringify(siteVisualKey(before))},
 cleanup:()=>{const before=constructionLights.children.length;for(let i=0;i<20;i++){const view=new HutConstruction(tiles,sampleSite(5),hutDefinition,20,20);constructionRoot.addChild(view);constructionLights.addChild(view.light);view.setNight(1);view.destroy({children:true})}return {before,after:constructionLights.children.length}},
 lightLevels:()=>constructionViews.map(view=>view.light.alpha),
 mapCheck:async()=>{
  const host=document.createElement('div');host.id='construction-map';host.style.cssText='width:640px;height:480px';document.body.appendChild(host)
  const world=await VillageWorld.create(host,{minimap:false})
  world.setCatalog(catalog)
  const state=structuredClone(result.state) as VillageState
  state.sites=[sampleSite(5)];state.structures=state.structures.filter(s=>s.plotId!=='8,8');state.storages=state.storages.filter(s=>s.plotId!=='8,8')
  world.setVillage(state);world.setNightOverride(false);world.centerOn(9.5,9.5)
  const internals=world as unknown as {buildingViews:Container[];buildingLights:Container;litBuildings:Set<Container>}
  const initial=internals.buildingViews.find(view=>view instanceof HutConstruction) as HutConstruction
  const attached=initial.light.parent===internals.buildingLights && initial.light.x===128 && initial.light.y===128
  state.sites[0].stages[5].jobs[0].done=1
  world.setVillage(state)
  const refreshed=internals.buildingViews.find(view=>view instanceof HutConstruction)
  const deliveryRefresh=refreshed!==initial && initial.destroyed && initial.light.destroyed
  state.sites=[];state.structures.push({kind:'Hut',plotId:'8,8',damage:0})
  world.setVillage(state)
  const completed=!internals.buildingViews.some(view=>view instanceof HutConstruction)
  const detached=refreshed!.destroyed
  // Keep the completed map alive briefly for a real renderer capture; caller can switch to night.
  Object.assign(window,{constructionMap:world})
  return {attached,deliveryRefresh,completed,detached}
 },
}})
