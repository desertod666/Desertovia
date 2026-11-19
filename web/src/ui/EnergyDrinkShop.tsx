import { DcoinIcon } from './DcoinIcon'
import {useEffect, useRef, useState} from 'react'
import {api, createOperationId, type Citizen, type EconomyView} from '../api'
import {useServerNow} from '../hooks'
import {useI18n} from '../i18n'

export function EnergyDrinkCard({view,citizen,busy,onBuy}:{view:EconomyView;citizen:Citizen;busy:boolean;onBuy:(currency:'coins'|'candles')=>void}) {
  const i18n=useI18n(), ru=i18n.language==='ru', now=useServerNow(0,1000)
  const cooling=!!view.energyDrinkReadyAt&&Date.parse(view.energyDrinkReadyAt)>now
  const unavailable=!!(citizen.exhaustedUntil&&Date.parse(citizen.exhaustedUntil)>now)||!!(citizen.injuredUntil&&Date.parse(citizen.injuredUntil)>now)||!!(citizen.recovery?.until&&Date.parse(citizen.recovery.until)>now)||citizen.recovery?.available===false
  return <section className="card energy-shop"><h3>{ru?'Энергетик батюшки':'Chaplain energy drink'}</h3>
    <p>{ru?'Сразу восстановить все силы. Раз в 12 реальных часов. Не лечит ранения и потерю сознания.':'Restore all energy immediately. Once per 12 real hours. Does not heal injuries or unconsciousness.'}</p>
    <p className="small"><DcoinIcon/> {view.coins} {ru?'Dcoin':'Dcoin'} · 🪙 {view.testCandles} {ru?'тестовых дезкоинов':'test descoins'}</p>
    {cooling&&<p role="status">{ru?'Снова доступен':'Available again'}: {new Date(view.energyDrinkReadyAt!).toLocaleString(i18n.language)}</p>}
    <div className="row">{(['coins','candles'] as const).map(currency=><button key={currency} disabled={busy||cooling||unavailable||citizen.energy>=citizen.maximumEnergy||(currency==='coins'?view.coins<300:view.testCandles<3)} onClick={()=>onBuy(currency)}>
      {ru?'Выпить':'Drink'} · {currency==='coins'?'300 '+(ru?'Dcoin':'Dcoin'):'3 '+(ru?'тестовых дезкоина':'test descoins')}
    </button>)}</div>
  </section>
}

export function EnergyDrinkShop({slug,citizen}:{slug:string;citizen:Citizen}) {
  const i18n=useI18n(), [view,setView]=useState<EconomyView|null>(null),[busy,setBusy]=useState(false),[error,setError]=useState('')
  const locked=useRef(false),requests=useRef(new Map<string,string>())
  useEffect(()=>{let active=true;api.economy(slug).then(v=>{if(active)setView(v)}).catch(e=>{if(active)setError(i18n.error(e))});return()=>{active=false}},[slug,i18n])
  const buy=async(currency:'coins'|'candles')=>{
    if(locked.current)return
    locked.current=true;setBusy(true);setError('')
    let requestId=requests.current.get(currency)
    if(!requestId){requestId=createOperationId();requests.current.set(currency,requestId)}
    try{setView(await api.economyAction(slug,{requestId,action:'energy-drink-'+currency}));requests.current.delete(currency)}
    catch(e){setError(i18n.error(e))}finally{locked.current=false;setBusy(false)}
  }
  return <>{error&&<p role="alert">{error}</p>}{view&&<EnergyDrinkCard view={view} citizen={citizen} busy={busy} onBuy={currency=>void buy(currency)}/>}</>
}
