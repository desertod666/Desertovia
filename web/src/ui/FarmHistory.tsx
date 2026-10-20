import {useEffect,useRef,useState} from 'react'
import {api,type FarmCropHistory} from '../api'
import {useI18n} from '../i18n'
import {FarmCare} from './FarmCare'

export function FarmHistory({slug,plotId,version}:{slug:string;plotId:string;version:string}) {
 const i18n=useI18n(),ru=i18n.language==='ru'
 const[open,setOpen]=useState(false),[items,setItems]=useState<FarmCropHistory[]>([]),[pending,setPending]=useState(false),[error,setError]=useState(''),[more,setMore]=useState(false)
 const generation=useRef(0)
 useEffect(()=>{
  if(!open)return
  const token=++generation.current
  setPending(true);setError('')
  api.farmHistory(slug,plotId).then(rows=>{if(token===generation.current){setItems(rows);setMore(rows.length===20)}})
   .catch(e=>{if(token===generation.current)setError(i18n.error(e))}).finally(()=>{if(token===generation.current)setPending(false)})
  return()=>{generation.current++}
 },[open,slug,plotId,version,i18n])
 const next=async()=>{
  if(pending||items.length===0)return
  const token=++generation.current;setPending(true);setError('')
  try{const rows=await api.farmHistory(slug,plotId,items.at(-1)!.id);if(token!==generation.current)return;setItems(previous=>[...new Map([...previous,...rows].map(row=>[row.id,row])).values()]);setMore(rows.length===20)}
  catch(e){if(token===generation.current)setError(i18n.error(e))}
  finally{if(token===generation.current)setPending(false)}
 }
 return <details className="farm-history" onToggle={e=>setOpen(e.currentTarget.open)}><summary>{ru?'История посевов':'Crop history'}</summary>
  {error&&<p className="error">{error}</p>}
  {pending&&<p className="small" role="status">{ru?'Загрузка истории…':'Loading history…'}</p>}
  {!pending&&!error&&items.length===0&&<p className="small">{ru?'Записанных посевов пока нет.':'No recorded crops yet.'}</p>}
  {items.map(crop=><details className="farm-history-crop" key={crop.id}><summary>{i18n.t('field.bed',{number:crop.bedIndex+1})} · {i18n.crop(crop.crop)} · {crop.status==='Harvested'?(ru?'собрано':'harvested'):crop.status==='Lost'?(ru?'посев утрачен':'crop lost'):(ru?'растёт':'growing')}</summary>
   <small>{new Date(crop.sownAt).toLocaleString(i18n.language)}</small><FarmCare actions={crop.contributions}/>
   {crop.received.length>0&&<p className="small">{ru?'Получено':'Received'}: {crop.received.map(g=>i18n.produced(g.resource,g.amount)).join(', ')}</p>}
   {crop.lost.length>0&&<p className="small">{ru?'Потеряно при сборе':'Lost at harvest'}: {crop.lost.map(g=>i18n.produced(g.resource,g.amount)).join(', ')}</p>}
  </details>)}
  {more&&<button type="button" disabled={pending} onClick={()=>void next()}>{ru?'Ещё посевы':'More crops'}</button>}
 </details>
}
