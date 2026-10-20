import {createRoot} from 'react-dom/client'
import {useState} from 'react'
import {WorldAtlas} from '../src/ui/WorldAtlas'
import type {WorldRoute,TilePoint} from '../src/api'
import {I18nProvider} from '../src/i18n'
import '../src/index.css'
declare global{interface Window{atlasFocus:TilePoint[]}}
window.atlasFocus=[]
const routes:WorldRoute[]=[{id:'settlement',district:'home',resources:[],danger:'low',destination:{x:15.5,y:13.5},blockedBy:null},{id:'deer-woods',district:'western-woods',resources:['Food','Leather'],danger:'moderate',destination:null,blockedBy:'unexplored'},{id:'ridge-cave',district:'northern-ridge',resources:['Coal','IronOre'],danger:'high',destination:null,blockedBy:'unexplored'},{id:'lookout',district:'northern-ridge',resources:[],danger:'moderate',destination:null,blockedBy:'unknown'},{id:'gold-vein',district:'far-bank',resources:['Gold'],danger:'high',destination:{x:40,y:17},blockedBy:'bridge'}]
function Stand(){const[open,setOpen]=useState(true);return open?<WorldAtlas routes={routes} onFocus={point=>window.atlasFocus.push(point)} onClose={()=>setOpen(false)}/>:<button onClick={()=>setOpen(true)}>Open atlas</button>}
createRoot(document.getElementById('app')!).render(<I18nProvider><Stand/></I18nProvider>)
