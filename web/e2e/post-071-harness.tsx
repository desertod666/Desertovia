import { createRoot } from 'react-dom/client'
import { useState } from 'react'
import { I18nProvider } from '../src/i18n'
import { CivicPanel } from '../src/ui/CivicPanel'
import { BattleControls } from '../src/ui/BattleControls'
import { WalletDialog, WalletHud } from '../src/ui/WalletHud'
import { BottomBar, TopBar } from '../src/ui/Hud'
import {SafeRestControl} from '../src/ui/SafeRestControl'
import {PersonalBattlePanel} from '../src/ui/PersonalBattlePanel'
import {actorFighter} from '../src/game/battleActor'
import {RoyalCombatSheet} from '../src/ui/RoyalCombatSheet'
import type { Catalog, Citizen, RaidBattle, RaidUnit, VillageState } from '../src/api'
import type {RestState} from '../src/api'
import '../src/index.css'
const unit={id:'target',variant:'weak',state:'Retreating',health:13,maximumHealth:13,exitedAt:null} as RaidUnit
const village={slug:'stand',resources:[{kind:'Wood',amount:10,capacity:30}],tsar:{name:'Царь',injuredUntil:null},structures:[{kind:'Hut',plotId:'15,16'}],storages:[],housing:[],civicOrders:[{id:'order',plotId:'15,16',operation:'upgrade',status:'pending'}],raids:[{id:'raid',enemy:'Zombie'}]} as unknown as VillageState
const battle={id:'raid',fighters:[],finishedAt:null} as unknown as RaidBattle
const legacyMode=new URLSearchParams(location.search).has('legacyFighter')
const royalMode=new URLSearchParams(location.search).has('royalCombat')
const royalVillage={...village,tsar:{...village.tsar,injuredUntil:new Date(Date.now()+600000).toISOString(),combat:{strength:0,dexterity:0,endurance:0,guarding:0,renown:0,weapon:'Bow',armor:'LeatherArmor',arrows:1,weaponSharpen:0,arrowSharpen:2,raidHealth:32,power:6}}} as VillageState
Object.assign(window,{royalStand:{initial:royalVillage}})
const legacyCitizen={id:'old-ruler-resident',load:[],personalGoods:[]} as unknown as Citizen
if(legacyMode){battle.phase='Fighting';battle.fighters=[{citizenId:legacyCitizen.id,accountId:null,isTsar:false,x:15.5,y:13.5,health:7,maximumHealth:32,damageDealt:3,kills:0,wounded:false,guard:false,active:true,mode:'hold',targetUnitId:null}]}
const hudMode=new URLSearchParams(location.search).has('hud')
const needsMode=new URLSearchParams(location.search).has('needs')
const restMode=new URLSearchParams(location.search).has('rest')
const hudData=hudMode?await Promise.all([fetch('/api/catalog').then(r=>r.json()),fetch('/api/villages/demo-bridge').then(r=>r.json())]):null
if(hudData)hudData[1].state.resources.forEach((r:{amount:number},i:number)=>{r.amount=100+i})
const citizen=needsMode?{id:'synthetic-citizen',accountId:'synthetic',energy:40,maximumEnergy:100,energyPerHour:5,serverNow:new Date().toISOString(),regenerationMultiplier:.5,shelterQuality:.5,isHungry:new URLSearchParams(location.search).has('hungry'),cycleTired:new URLSearchParams(location.search).has('tired'),cyclesSinceMeal:1,state:'Idle',injuredUntil:null,drinkReadyAt:null} as Citizen:null
function Stand(){const [closed,close]=useState(false),[wallet,setWallet]=useState(false),[rest,setRest]=useState<RestState>('Active')
 const [royal,setRoyal]=useState(royalVillage)
 if(royalMode)return <div className="card" style={{maxWidth:420,margin:8}}><RoyalCombatSheet village={royal} now={Date.now()} onVillage={setRoyal}/></div>
 if(legacyMode){const fighter=actorFighter(battle.fighters,'owner',legacyCitizen.id)!;return <div className="game-root"><BattleControls village={village} battle={battle} unit={unit} citizen={legacyCitizen} catalog={{tasks:[]} as unknown as Catalog} tsar accountId="owner" onClose={()=>{}}/><div style={{position:'absolute',bottom:12,left:8}}><PersonalBattlePanel slug="stand" battle={battle} fighter={fighter} remedies={2} now={Date.now()} onError={message=>{throw new Error(message)}}/></div></div>}
 if(hudData)return <div className="game-root"><div className="hud"><TopBar village={hudData[1].state} catalog={hudData[0]} citizen={citizen} citizens={new Map()} onForecast={async()=>{}} rest={restMode?<SafeRestControl state={rest} onChange={async resting=>setRest(resting?'Resting':'Active')}/>:undefined} wallet={<WalletHud slug="stand" onOpen={()=>setWallet(true)}/>}/>{needsMode&&<BottomBar citizen={citizen} catalog={hudData[0]} structures={hudData[1].state.structures} now={Date.now()} phone={innerWidth<=720} tsar={false} ruler={false} hasResult={false} poll={null} onMenu={()=>{}} onInventory={()=>{}} onShowResult={()=>{}} pending={false} onCancelTask={()=>{}} drinks={0} onDrink={()=>{}} remedies={0} onHeal={()=>{}}/>}</div></div>
 return <><div style={{padding:8,background:'var(--parchment)',color:'var(--ink)',minHeight:'100dvh'}}><WalletHud slug="stand" onOpen={()=>setWallet(true)}/>{wallet&&<WalletDialog village={village} catalog={{tasks:[],buildings:[]} as unknown as Catalog} citizen={null} accountId="owner" onClose={()=>setWallet(false)} onPrivatePlace={()=>{}}/>}<CivicPanel village={village} tsar ruler actorId="owner" onVillage={()=>{}}/></div>{!closed&&<BattleControls village={village} battle={battle} unit={unit} citizen={null} catalog={{tasks:[]} as unknown as Catalog} tsar accountId="owner" onClose={()=>close(true)}/>}</>
}
createRoot(document.getElementById('root')!).render(<I18nProvider><Stand/></I18nProvider>)
