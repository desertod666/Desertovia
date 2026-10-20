import {createRoot} from 'react-dom/client'
import {useState} from 'react'
import {api,type Catalog,type Citizen,type FarmWorkTarget,type VillageState} from '../src/api'
import {I18nProvider} from '../src/i18n'
import {TargetPopup} from '../src/ui/TargetPopup'
import '../src/index.css'

declare global {interface Window {farmCalls:unknown[]}}
window.farmCalls=[]
const now=Date.now(),iso=new Date(now).toISOString()
const village={id:'farm',slug:'farm-fixture',structures:[{kind:'Farm',plotId:'21,12',damage:0,level:1,number:1}],sites:[],storages:[],resources:[{kind:'Food',amount:30,capacity:1000}],fields:[
 {plotId:'21,12',index:0,generationId:'11111111-1111-4111-8111-111111111111',crop:'Turnip',growth:1,wateredUntil:null,thirsty:false},
 {plotId:'21,12',index:1,generationId:'22222222-2222-4222-8222-222222222222',crop:'Carrot',growth:.3,wateredUntil:null,thirsty:true},
 {plotId:'21,12',index:2,generationId:'33333333-3333-4333-8333-333333333333',crop:null,growth:0,wateredUntil:null,thirsty:false}],
 weather:{raining:false},completedProjects:[],technologies:[],wildlife:[],goldVeinRemaining:1000,project:null,kingdomArea:{x:0,y:0,width:28,height:26}} as unknown as VillageState
const citizen={id:'farmer',accountId:'worker',name:'Farmer',state:'Idle',serverNow:iso,cycleEndsAt:null,energy:36,maximumEnergy:36,skills:[{skill:'Farming',level:2}],load:[],personalGoods:[],armor:null,weapon:null,carryLimit:20} as unknown as Citizen
function FarmStand({catalog}:{catalog:Catalog}) {
 const[pending,setPending]=useState(false)
 const start=(task:Catalog['tasks'][number],_spot:unknown,plot?:string,target?:FarmWorkTarget)=>{window.farmCalls.push({taskId:task.id,workplacePlotId:plot,farmTarget:target});setPending(true)}
 return <TargetPopup target={{kind:'building',building:'Farm',plotId:'21,12',area:null,storage:false}} screen={{x:160,y:100}} phone={innerWidth<=720} village={village} catalog={catalog} citizen={citizen} now={now} pending={pending} error={null} onStart={start} onStartJob={()=>{}} ruler={false} onCivic={async()=>{}} onTake={()=>{}} onClose={()=>{}}/>
}
api.catalog().then(catalog=>createRoot(document.getElementById('app')!).render(<I18nProvider><FarmStand catalog={catalog}/></I18nProvider>))
