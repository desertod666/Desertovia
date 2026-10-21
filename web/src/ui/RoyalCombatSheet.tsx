import {useRef,useState} from 'react'
import {api,createOperationId,type VillageState} from '../api'
import {useI18n} from '../i18n'

/** Details live inside the ruler's menu, without another permanent HUD panel. */
export function RoyalCombatSheet({village,now,onVillage}:{village:VillageState;now:number;onVillage:(state:VillageState)=>void}) {
 const i18n=useI18n(),ru=i18n.language==='ru',body=village.tsar.combat
 const [busy,setBusy]=useState(false),[error,setError]=useState(''),request=useRef<string|null>(null)
 if(!body)return null
 const wounded=!!village.tsar.injuredUntil&&Date.parse(village.tsar.injuredUntil)>now
 const heal=async()=>{
  if(busy)return
  setBusy(true);setError('');request.current??=createOperationId()
  try{onVillage(await api.healRoyal(village.slug,request.current));request.current=null}
  catch(reason){setError(i18n.error(reason))}finally{setBusy(false)}
 }
 return <details className="royal-combat-sheet"><summary>{ru?'Боевой герой':'Combat hero'} · ♥ {body.raidHealth}{wounded?` · ${ru?'Ранен':'Wounded'}`:''}</summary>
  <dl className="character-facts"><dt>{ru?'Сила / ловкость / выносливость':'Strength / dexterity / endurance'}</dt><dd>{body.strength} / {body.dexterity} / {body.endurance}</dd>
   <dt>{ru?'Охрана / слава':'Guarding / renown'}</dt><dd>{body.guarding} / {body.renown}</dd>
   <dt>{ru?'Оружие':'Weapon'}</dt><dd>{body.weapon?i18n.resource(body.weapon):ru?'Без оружия':'Unarmed'}</dd>
   <dt>{ru?'Защита':'Armor'}</dt><dd>{body.armor?i18n.resource(body.armor):'—'}</dd>
   {body.weapon==='Bow'&&<><dt>{ru?'Стрелы':'Arrows'}</dt><dd>{body.arrows}</dd></>}
  </dl>
  <p className="small">{ru?'HP для нового рейда. Уже начатые бои сохраняют свои условия.':'HP for a new raid. Existing battles keep their accepted rules.'}</p>
  {wounded&&<><p role="status">{ru?'Ранение до':'Wounded until'} {new Date(village.tsar.injuredUntil!).toLocaleTimeString(i18n.language,{hour:'2-digit',minute:'2-digit'})}</p>
   <button disabled={busy} onClick={()=>void heal()}>{busy?(ru?'Лечим…':'Healing…'):ru?'Использовать лекарство':'Use remedy'}</button></>}
  {error&&<p role="alert" className="error">{error}</p>}
 </details>
}
