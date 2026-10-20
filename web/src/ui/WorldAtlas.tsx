import {useEffect,useState} from 'react'
import type {TilePoint,WorldRoute} from '../api'
import {useI18n} from '../i18n'
import {Hint} from './Hint'
import {ResourceIcon} from './TileIcon'

const names:Record<string,[string,string]>={settlement:['Поселение','Settlement'],'deer-woods':['Оленьи леса','Deer woods'],'ridge-cave':['Горная пещера','Ridge cave'],lookout:['Дозорная вершина','Lookout peak'],'gold-vein':['Золотая жила','Gold vein']}
const districts:Record<string,[string,string]>={home:['Обжитые земли','Home lands'],'western-woods':['Западные леса','Western woods'],'northern-ridge':['Северный кряж','Northern ridge'],'far-bank':['Дальний берег','Far bank']}

/** Atlas is a camera/navigation aid. It never issues a walk, enters combat, reveals fog or awards loot. */
export function WorldAtlas({routes,onFocus,onClose}:{routes:WorldRoute[];onFocus:(point:TilePoint)=>void;onClose:()=>void}){
 const i18n=useI18n(),ru=i18n.language==='ru',language=ru?0:1
 const[selected,setSelected]=useState('settlement'),route=routes.find(r=>r.id===selected)??routes[0]
 useEffect(()=>{const key=(event:KeyboardEvent)=>{if(event.key==='Escape'&&!document.querySelector('[role="tooltip"]')){event.preventDefault();event.stopImmediatePropagation();onClose()}};window.addEventListener('keydown',key,true);return()=>window.removeEventListener('keydown',key,true)},[onClose])
 const name=(item:WorldRoute)=>names[item.id]?.[language]??item.id
 const reason=route?.blockedBy==='bridge'?(ru?'Нужен царский мост.':'The Tsar Bridge is required.'):route?.blockedBy==='unexplored'?(ru?'Сначала разведайте этот район.':'Explore this district first.'):route?.blockedBy==='unknown'?(ru?'Место ещё не известно.':'This place is not known yet.'):ru?'Место разведано.':'This place is explored.'
 return <div className="modal-backdrop" onPointerDown={event=>{if(event.target===event.currentTarget)onClose()}}><section className="modal world-atlas" role="dialog" aria-modal="true" aria-label={ru?'Округа':'District atlas'}>
  <header className="modal-header"><h2>{ru?'Округа':'District atlas'}</h2><Hint className="help-button" label={ru?'О карте округи':'About the district atlas'} text={ru?'Обзор существующих мест. Показать на карте перемещает только камеру. Герой идёт обычным путём; мост, туман и опасности сохраняются. Ресурсы обозначают занятия района, а не награду за нажатие.':'Overview of existing places. Show on map moves only the camera. Your hero travels normally; the bridge, fog and dangers still apply. Resources describe district activities, not rewards for clicking.'}>ⓘ</Hint><button className="close" aria-label={i18n.t('close')} onClick={onClose}>✕</button></header>
  <div className="modal-body"><nav className="world-route-list" aria-label={ru?'Места округи':'District places'}>{routes.map(item=><button key={item.id} aria-pressed={item.id===route?.id} onClick={()=>setSelected(item.id)}><strong>{name(item)}</strong><small>{districts[item.district]?.[language]??item.district}</small><span aria-hidden="true">{item.destination?'◈':'?'}</span></button>)}</nav>
   {route&&<section className="world-route-detail"><h3>{name(route)}</h3><p className="small">{reason}</p><p className="small">{ru?'Опасность района':'District danger'}: {route.danger==='high'?(ru?'высокая':'high'):route.danger==='moderate'?(ru?'средняя':'moderate'):(ru?'низкая':'low')}</p>
    {route.resources.length>0&&<div className="world-route-resources">{route.resources.map(resource=><span key={resource}><ResourceIcon kind={resource} scale={1}/>{i18n.resource(resource)}</span>)}</div>}
    <button disabled={!route.destination} onClick={()=>{if(route.destination){onFocus(route.destination);onClose()}}}>{ru?'Показать на карте':'Show on map'}</button>
   </section>}
  </div>
 </section></div>
}
