import { useEffect, useRef, useState } from 'react'
import { api, type BuildingDefinition, type Catalog, type Citizen, type VillageState } from '../api'
import { useI18n } from '../i18n'
import { EconomicGoldIcon } from './EconomicGoldIcon'
import { EconomyPanel } from './EconomyPanel'

/** The existing server wallet, refreshed independently of the combat animation tick. */
export function WalletHud({slug,onOpen}:{slug:string;onOpen:()=>void}) {
  const i18n=useI18n(),ru=i18n.language==='ru'
  const [balance,setBalance]=useState<{coins:number;testCandles:number}|null>(null),[failed,setFailed]=useState(false)
  const epoch=useRef(0)
  useEffect(()=>{
    let active=true,inFlight=false
    setBalance(null);setFailed(false);epoch.current++
    const update=async()=>{
      if(inFlight||document.hidden)return
      inFlight=true;const version=epoch.current
      try{const data=await api.economy(slug);if(active&&version===epoch.current){setBalance(data);setFailed(false)}}
      catch{if(active&&version===epoch.current)setFailed(true)}finally{inFlight=false}
    }
    const changed=(event:Event)=>{
      const detail=(event as CustomEvent<{slug:string;coins:number;testCandles:number}>).detail
      if(detail?.slug!==slug)return
      epoch.current++;setBalance({coins:detail.coins,testCandles:detail.testCandles});setFailed(false)
    }
    void update();const timer=window.setInterval(()=>void update(),15000)
    const refresh=()=>void update()
    window.addEventListener('desertovia-wallet-changed',changed);window.addEventListener('focus',refresh);window.addEventListener('online',refresh);document.addEventListener('visibilitychange',refresh)
    return()=>{active=false;window.clearInterval(timer);window.removeEventListener('desertovia-wallet-changed',changed);window.removeEventListener('focus',refresh);window.removeEventListener('online',refresh);document.removeEventListener('visibilitychange',refresh)}
  },[slug])
  const amount=(n:number)=>new Intl.NumberFormat(i18n.language,{notation:n>=100000?'compact':'standard',maximumFractionDigits:1}).format(n)
  return <button className="wallet-hud" onClick={onOpen} aria-label={i18n.t('wallet.open')} title={balance?`${i18n.t('currency.gold')}: ${balance.coins} · ${ru?'Тестовые свечи':'Test candles'}: ${balance.testCandles}`:ru?'Кошелёк':'Wallet'}>
    <span><EconomicGoldIcon/><strong>{balance?amount(balance.coins):failed?'—':'…'}</strong><span className="wallet-label"> {i18n.t('currency.gold')}</span></span>
    <span><span aria-hidden="true">🕯</span><strong>{balance?amount(balance.testCandles):failed?'—':'…'}</strong><span className="wallet-label"> {ru?'свечи':'candles'}</span><small>{ru?'тест':'test'}</small></span>
    {failed&&<span role="status" className="wallet-stale" title={ru?'Не удалось обновить баланс':'Could not refresh balance'}>↻</span>}
  </button>
}

export function WalletDialog({village,catalog,citizen,accountId,onClose,onPrivatePlace}:{village:VillageState;catalog:Catalog;citizen?:Citizen|null;accountId:string;onClose:()=>void;onPrivatePlace:(building:BuildingDefinition)=>void}) {
  const i18n=useI18n(),ru=i18n.language==='ru'
  useEffect(()=>{const key=(e:KeyboardEvent)=>{if(e.key==='Escape')onClose()};window.addEventListener('keydown',key);return()=>window.removeEventListener('keydown',key)},[onClose])
  return <div className="modal-backdrop" onPointerDown={e=>{if(e.target===e.currentTarget)onClose()}}><section className="modal wallet-dialog" role="dialog" aria-modal="true" aria-label={ru?'Кошелёк':'Wallet'}>
    <header className="modal-header"><h2>{ru?'Кошелёк':'Wallet'}</h2><button className="close" aria-label={i18n.t('close')} onClick={onClose}>✕</button></header>
    <div className="modal-body"><EconomyPanel village={village} catalog={catalog} citizen={citizen} accountId={accountId} initialOpen onPrivatePlace={onPrivatePlace}/></div>
    <footer className="menu-actions"><button onClick={onClose}>{ru?'Вернуться к карте':'Back to map'}</button></footer>
  </section></div>
}
