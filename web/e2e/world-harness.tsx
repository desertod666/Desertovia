import {createRoot} from 'react-dom/client'
import {useState} from 'react'
import {WorldAtlas} from '../src/ui/WorldAtlas'
import type {WorldRoute,TilePoint,WalkJourney} from '../src/api'
import {JourneyControl} from '../src/ui/JourneyControl'
import {I18nProvider} from '../src/i18n'
import '../src/index.css'
import {buildLayout,buildLegacyLayout,findWay,MAP_WIDTH,MAP_HEIGHT,LEGACY_MAP_HEIGHT,Terrain} from '../src/game/worldMap'
import type {RegionInspection} from './regional-contract'
import './journey-contract'
declare global{interface Window{atlasFocus:TilePoint[]}}
window.atlasFocus=[]
window.atlasTravel=[]
window.inspectRegions=async seed=>{
 const start=performance.now(),old=buildLegacyLayout(seed,null),world=buildLayout(seed,null)
 const bytes=(value:Uint8Array)=>crypto.subtle.digest('SHA-256',value as Uint8Array<ArrayBuffer>).then(buffer=>[...new Uint8Array(buffer)].map(b=>b.toString(16).padStart(2,'0')).join(''))
 const legacy=world.terrain.slice(0,MAP_WIDTH*LEGACY_MAP_HEIGHT)
 if(!legacy.every((kind,index)=>kind===old.terrain[index]))throw Error('Old terrain changed')
 const mask=legacy.map(kind=>[Terrain.Water,Terrain.Mountain,Terrain.Ravine].includes(kind as 2|7|8)?1:kind===Terrain.Marsh?2:0)
 const extra=world.terrain.slice(legacy.length),counts:Record<string,number>={}
 for(const kind of extra)counts[kind]=(counts[kind]??0)+1
 const reachable=Array.isArray(findWay(world.terrain,{x:15.5,y:57.5},{x:14.5,y:92.5},false))
 const waterBlocked=typeof findWay(world.terrain,{x:14.5,y:92.5},{x:63.5,y:83.5},true)==='string'
 return{legacyMaskHash:await bytes(mask),legacySize:legacy.length,size:MAP_WIDTH*MAP_HEIGHT,extraHash:await bytes(extra),milliseconds:performance.now()-start,reachable,waterBlocked,marshTiles:counts[Terrain.Marsh]??0,extraCounts:counts} satisfies RegionInspection
}
const routes:WorldRoute[]=[{id:'settlement',district:'home',resources:[],danger:'low',destination:{x:15.5,y:13.5},blockedBy:null},{id:'deer-woods',district:'western-woods',resources:['Food','Leather'],danger:'moderate',destination:null,blockedBy:'unexplored'},{id:'ridge-cave',district:'northern-ridge',resources:['Coal','IronOre'],danger:'high',destination:null,blockedBy:'unexplored'},{id:'lookout',district:'northern-ridge',resources:[],danger:'moderate',destination:null,blockedBy:'unknown'},{id:'gold-vein',district:'far-bank',resources:['Gold'],danger:'high',destination:{x:40,y:17},blockedBy:'bridge'}]
function Stand(){const[open,setOpen]=useState(true);const regional=new URLSearchParams(location.search).has('regions');return open?<WorldAtlas routes={regional?[...routes,{id:'southern-marsh',district:'southern-lowlands',resources:['Reeds','Food'],danger:'moderate',destination:null,blockedBy:'unexplored',travelTarget:{x:14.5,y:92.5}},{id:'southern-quarry',district:'southern-uplands',resources:['Stone'],danger:'high',destination:null,blockedBy:'bridge',travelTarget:{x:54.5,y:104.5}}]:routes} onFocus={point=>window.atlasFocus.push(point)} onClose={()=>setOpen(false)} onTravel={regional?async point=>{window.atlasTravel.push(point)}:undefined}/>:<button onClick={()=>setOpen(true)}>Open atlas</button>}
window.journeyRequests=[];window.journeyAtlasOpened=false;window.journeyCancelled=false
function JourneyStand(){
 const stop='2026-01-01T12:00:03Z'
 const[now,setNow]=useState(Date.parse(new URLSearchParams(location.search).has('future')?'2026-01-01T12:00:01Z':stop))
 const[journey,setJourney]=useState<WalkJourney|null>({id:'00000000-0000-0000-0000-000000000123',from:{x:0,y:0},waypoints:[{x:10,y:0,cost:1}],setOutAt:'2026-01-01T12:00:00Z',arrivesAt:'2026-01-01T12:00:06Z',snapshotAt:stop,speedMultiplier:1,blockedAt:stop,blockReason:'gate'})
 return <><button onClick={()=>setNow(Date.parse(stop))}>Reach obstruction</button><JourneyControl journey={journey} now={now} onAtlas={()=>{window.journeyAtlasOpened=true}} onCancel={async()=>{window.journeyCancelled=true;setJourney(null)}} onResume={async body=>{
  window.journeyRequests.push(body)
  // Simulate an uncertain first reply; a manual retry must retain the same request body.
  if(window.journeyRequests.length===1)throw Error('Connection lost')
  setJourney(null)
 }}/></>
}
createRoot(document.getElementById('app')!).render(<I18nProvider>{new URLSearchParams(location.search).has('journey')?<JourneyStand/>:<Stand/>}</I18nProvider>)
