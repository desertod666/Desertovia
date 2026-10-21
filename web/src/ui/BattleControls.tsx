import { useCallback, useEffect, useRef, useState, type RefObject } from 'react'
import { api, type Catalog, type Citizen, type RaidBattle, type RaidUnit, type VillageState } from '../api'
import { useI18n } from '../i18n'
import { actorFighter } from '../game/battleActor'

export function BattleControls({ village, battle, unit, citizen, tsar=false, accountId, onClose, selectionToken=0, issuedOrder }: {
  village: VillageState; battle: RaidBattle; unit: RaidUnit; citizen: Citizen | null | undefined;
  catalog: Catalog; tsar?:boolean; accountId?:string; onDefend?:()=>void; onClose: () => void
  selectionToken?:number; issuedOrder?:RefObject<string>
}) {
  const i18n=useI18n(), ru=i18n.language==='ru'
  const [busy,setBusy]=useState(false),[error,setError]=useState('')
  const [now,setNow]=useState(()=>Date.now())
  useEffect(()=>{const timer=window.setInterval(()=>setNow(Date.now()),1000);return()=>window.clearInterval(timer)},[])
  const fighter=actorFighter(battle.fighters,accountId,citizen?.id)
  const alive=unit.health>0&&!unit.exitedAt&&!battle.finishedAt
  const recovering=tsar&&!!village.tsar.injuredUntil&&Date.parse(village.tsar.injuredUntil)>now
  const canOrder=(tsar||!!citizen)&&alive&&!recovering&&(!fighter||fighter.health>0)
  const raid=village.raids.find(r=>r.id===battle.id)
  const phases:Record<string,string>=ru?{Waiting:'Ожидает',Approaching:'Приближается',Fighting:'В бою',Raiding:'Разоряет',Breaching:'Ломает забор',Retreating:'Отступает',Escaped:'Ушёл',Dispersed:'Рассеялся',Dead:'Повержен'}:{}
  const localAttempt=useRef('')
  const attempted=issuedOrder??localAttempt
  const order=useCallback(async()=>{
    setBusy(true);setError('')
    try{await api.battleOrder(village.slug,battle.id,unit.id,'intercept')}catch(e){setError(i18n.error(e))}finally{setBusy(false)}
  },[village.slug,battle.id,unit.id,i18n])
  useEffect(()=>{
    const key=battle.id+':'+unit.id+':'+selectionToken
    if(!canOrder||attempted.current===key)return
    attempted.current=key;void order()
  },[canOrder,battle.id,unit.id,selectionToken,attempted,order])
  return <aside className="battle-controls" aria-label={ru?'Выбранный противник':'Selected enemy'}>
    <header><strong>{unit.enemy?i18n.enemy(unit.enemy):raid?i18n.enemy(raid.enemy):ru?'Противник':'Enemy'} · {ru?({weak:'Слабый',regular:'Обычный',strong:'Сильный',boss:'Босс'}[unit.variant]):unit.variant}</strong><button type="button" className="close" aria-label={i18n.t('close')} onClick={e=>{e.stopPropagation();onClose()}}><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/></svg></button></header>
    <p className="small">{phases[unit.state]??unit.state} · ♥ {Math.ceil(unit.health)}/{Math.ceil(unit.maximumHealth)}</p>
    <progress value={unit.health} max={unit.maximumHealth} aria-label={ru?'Здоровье врага':'Enemy health'}/>
    {fighter&&<p className="small">{ru?'Моё здоровье':'My health'}: {Math.ceil(fighter.health)}/{fighter.maximumHealth} · {ru?'Урон':'Damage'} {Math.round(fighter.damageDealt)}{fighter.arrows?` · ➶ ${fighter.arrows}`:''}</p>}
    {recovering&&<p role="status">{ru?'Царь на лечении до':'The Tsar is recovering until'} {new Date(village.tsar.injuredUntil!).toLocaleTimeString(i18n.language,{hour:'2-digit',minute:'2-digit'})}</p>}
    {!canOrder&&fighter&&fighter.health<=0&&<p>{ru?'Вы ранены. Дождитесь восстановления.':'You are wounded. Wait for recovery.'}</p>}
    {canOrder&&!error&&<p className="small" role="status">{busy?(ru?'Выбираю цель…':'Selecting target…'):(ru?'Цель выбрана. Подойду и буду бить автоматически.':'Target selected. I will approach and attack automatically.')}</p>}
    {error&&<p className="error small" role="status">{error}</p>}
    {canOrder&&error&&<button disabled={busy} onClick={()=>void order()}>{ru?'Повторить атаку':'Retry attack'}</button>}
  </aside>
}
