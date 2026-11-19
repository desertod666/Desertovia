import { createRoot } from 'react-dom/client'
import { useState } from 'react'
import { HospitalPanel } from '../src/ui/HospitalPanel'
import { I18nProvider } from '../src/i18n/components'
import { api, type HospitalPayment, type HospitalStatus, type VillageState } from '../src/api'
import '../src/index.css'

let paid = false
const until = new Date(Date.now() + 24 * 3600000).toISOString()
const recovery = { episodeId: 'duel-test', admittedAt: new Date().toISOString(), until, checkedAt: new Date().toISOString(), available:true, hospitalPercent:4, skillPercent:2 }
const status: HospitalStatus = { available:true, level:2, balance:200, recovery,
  tiers:[{code:'candle',title:'Благословение за здравие',minHours:1,maxHours:2,price:50,blocked:''},{code:'prayer',title:'Молитва',minHours:3,maxHours:4,price:100,blocked:''}],
  patients:[{id:'patient',name:'Игрок с длинным именем',appearance:{gender:'Male',style:0,skinTone:0,hairColor:0,outfitColor:0},recovery}] }
api.hospital = async () => paid ? {...status,patients:[],recovery:null,balance:150} : status
let firstId: string | null = null
api.hospitalHeal = async (_slug: string, request: HospitalPayment) => {
  const calls = document.getElementById('calls')!
  calls.textContent = String(Number(calls.textContent) + 1)
  document.getElementById('operation')!.textContent=request.requestId
  if (firstId === null) { firstId=request.requestId; return {state:'pending',error:null,until:null,price:50,cutSeconds:null} }
  if (request.requestId !== firstId) throw Error('Retry replaced the receipt!')
  paid=true
  return {state:'completed',error:null,until:null,price:50,cutSeconds:3600}
}
const village={structures:[{kind:'Hospital',plotId:'20,15',damage:0,level:2}],sites:[]} as unknown as VillageState
function Harness() {
 const [open,setOpen]=useState(true)
 return <I18nProvider><output id="calls">0</output><output id="operation"/>
  <button id="reopen" onClick={()=>setOpen(true)}>Reopen</button>
  {open && <HospitalPanel slug="test" actorId="actor" citizenId="patient" tsar={false} village={village} onVillage={()=>{}} onClose={()=>setOpen(false)} onUpdated={()=>{}} />}
 </I18nProvider>
}
createRoot(document.getElementById('app')!).render(<Harness/>);
