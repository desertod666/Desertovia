import {createRoot} from 'react-dom/client'
import {useState} from 'react'
import {WorldAtlas} from '../src/ui/WorldAtlas'
import type {WorldRoute,TilePoint} from '../src/api'
import {I18nProvider} from '../src/i18n'
import '../src/index.css'
import {buildLayout,buildLegacyLayout,findWay,MAP_WIDTH,MAP_HEIGHT,LEGACY_MAP_HEIGHT,Terrain} from '../src/game/worldMap'
import type {RegionInspection} from './regional-contract'
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
createRoot(document.getElementById('app')!).render(<I18nProvider><Stand/></I18nProvider>)
