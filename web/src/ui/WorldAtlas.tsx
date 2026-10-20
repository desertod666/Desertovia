import {useEffect,useState} from 'react'
import type {TilePoint,WorldRoute} from '../api'
import {useI18n} from '../i18n'
import {Hint} from './Hint'
import {ResourceIcon} from './TileIcon'

const names:Record<string,[string,string]>={settlement:['Поселение','Settlement'],'deer-woods':['Оленьи леса','Deer woods'],'ridge-cave':['Горная пещера','Ridge cave'],lookout:['Дозорная вершина','Lookout peak'],'gold-vein':['Золотая жила','Gold vein'],'southern-marsh':['Южные болота','Southern marsh'],'southern-quarry':['Южный каменный кряж','Southern quarry']}
const districts:Record<string,[string,string]>={home:['Обжитые земли','Home lands'],'western-woods':['Западные леса','Western woods'],'northern-ridge':['Северный кряж','Northern ridge'],'far-bank':['Дальний берег','Far bank'],'southern-lowlands':['Южная низина','Southern lowlands'],'southern-uplands':['Южное нагорье','Southern uplands']}

/** Looking moves only the camera. Explicit regional travel uses the ordinary authoritative movement command. */
export function WorldAtlas({routes,onFocus,onClose,onTravel,canTravel=true}:{routes:WorldRoute[];onFocus:(point:TilePoint)=>void;onClose:()=>void;onTravel?:(point:TilePoint)=>Promise<void>;canTravel?:boolean}){
 const i18n=useI18n(),ru=i18n.language==='ru',language=ru?0:1
 const[selected,setSelected]=useState('settlement'),route=routes.find(r=>r.id===selected)??routes[0]
 const[pending,setPending]=useState(false),[error,setError]=useState('')
 useEffect(()=>{const key=(event:KeyboardEvent)=>{if(event.key==='Escape'&&!document.querySelector('[role="tooltip"]')){event.preventDefault();event.stopImmediatePropagation();onClose()}};window.addEventListener('keydown',key,true);return()=>window.removeEventListener('keydown',key,true)},[onClose])
 const name=(item:WorldRoute)=>names[item.id]?.[language]??item.id
 const reason=route?.blockedBy==='bridge'?(ru?'Нужен царский мост.':'The Tsar Bridge is required.'):route?.blockedBy==='unexplored'?(ru?'Сначала разведайте этот район.':'Explore this district first.'):route?.blockedBy==='unknown'?(ru?'Место ещё не известно.':'This place is not known yet.'):ru?'Место разведано.':'This place is explored.'
 return <div className="modal-backdrop" onPointerDown={event=>{if(event.target===event.currentTarget)onClose()}}><section className="modal world-atlas" role="dialog" aria-modal="true" aria-label={ru?'Округа':'District atlas'}>
  <header className="modal-header"><h2>{ru?'Округа':'District atlas'}</h2><Hint className="help-button" label={ru?'О карте округи':'About the district atlas'} text={ru?'Обзор существующих мест. Показать на карте перемещает только камеру. Герой идёт обычным путём; мост, туман и опасности сохраняются. Ресурсы обозначают занятия района, а не награду за нажатие.':'Overview of existing places. Show on map moves only the camera. Your hero travels normally; the bridge, fog and dangers still apply. Resources describe district activities, not rewards for clicking.'}>ⓘ</Hint><button className="close" aria-label={i18n.t('close')} onClick={onClose}>✕</button></header>
  <div className="modal-body"><label className="world-route-picker">{ru?'Место':'Destination'}<select value={route?.id??''} onChange={event=>{setSelected(event.target.value);setError('')}}>{routes.map(item=><option key={item.id} value={item.id}>{name(item)}</option>)}</select></label><nav className="world-route-list" aria-label={ru?'Места округи':'District places'}>{routes.map(item=><button key={item.id} aria-pressed={item.id===route?.id} onClick={()=>{setSelected(item.id);setError('')}}><strong>{name(item)}</strong><small>{districts[item.district]?.[language]??item.district}</small><span aria-hidden="true">{item.destination?'◈':'?'}</span></button>)}</nav>
   {route&&<section className="world-route-detail"><h3>{name(route)}</h3><p className="small">{reason}</p><p className="small">{ru?'Опасность района':'District danger'}: {route.danger==='high'?(ru?'высокая':'high'):route.danger==='moderate'?(ru?'средняя':'moderate'):(ru?'низкая':'low')}</p>
    {route.resources.length>0&&<div className="world-route-resources">{route.resources.map(resource=><span key={resource}><ResourceIcon kind={resource} scale={1}/>{i18n.resource(resource)}</span>)}</div>}
    <button disabled={!route.destination} onClick={()=>{if(route.destination){onFocus(route.destination);onClose()}}}>{ru?'Показать на карте':'Show on map'}</button>
    {route.travelTarget&&onTravel&&<><p className="small muted">{ru?'Тропа известна. Землю откроете по дороге; сбор ресурсов — отдельная работа.':'The trail is known. Land is revealed as you walk; gathering is a separate task.'}</p><button disabled={pending||!canTravel||route.blockedBy==='bridge'} onClick={async()=>{setPending(true);setError('');try{await onTravel(route.travelTarget!);onClose()}catch(e){setError(i18n.error(e))}finally{setPending(false)}}}>{ru?'Идти по тропе':'Walk the trail'}</button></>}
    {error&&<p role="alert" className="error">{error}</p>}
   </section>}
  </div>
 </section></div>
}
