import {useRef,useState} from 'react'
import {createOperationId,type WalkJourney,type ResumeJourneyRequest} from '../api'
import {useI18n} from '../i18n'
import {Hint} from './Hint'

export function JourneyControl({journey,now,onResume,onAtlas,onCancel}:{journey:WalkJourney|null|undefined;now:number;onResume:(body:ResumeJourneyRequest)=>Promise<void>;onAtlas:()=>void;onCancel?:()=>Promise<void>}){
 const i18n=useI18n(),ru=i18n.language==='ru'
 const [busy,setBusy]=useState(false),[error,setError]=useState('')
 const request=useRef<{key:string;body:ResumeJourneyRequest}|null>(null)
 if(!journey?.blockedAt)return null
 const waiting=Date.parse(journey.blockedAt)<=now
 const cause=journey.blockReason==='bridge'?(ru?'Мост недоступен':'Bridge unavailable'):journey.blockReason==='gate'?(ru?'Ворота закрыты':'Gate closed'):(ru?'Путь перекрыт':'Route blocked')
 return <section className="journey-control card" aria-label={ru?'Дорога героя':'Hero journey'}>
  <div className="row"><strong>{cause}</strong><Hint label={ru?'О перекрытой дороге':'About the blocked route'} text={ru?'Герой остановится у препятствия. Ожидание не выполняет работу. После открытия пути продолжите дорогу; принятую работу можно отменить по обычным правилам.':'Your hero stops at the obstacle. Waiting does not complete work. Continue after the path opens; accepted work can be cancelled under the usual rules.'}>ⓘ</Hint></div>
  <p className="small" role="status">{waiting?(ru?'Герой ждёт продолжения пути.':'Your hero is waiting to continue.'):(ru?'Герой идёт до препятствия.':'Your hero is approaching the obstacle.')}</p>
  <div className="row"><button disabled={busy||!waiting} onClick={async()=>{
   const key=`${journey.id??''}:${journey.blockedAt}`
   if(request.current?.key!==key)request.current={key,body:{requestId:createOperationId(),journeyId:journey.id??'00000000-0000-0000-0000-000000000000',blockedAt:journey.blockedAt!}}
   setBusy(true);setError('');try{await onResume(request.current.body);request.current=null}catch(e){setError(i18n.error(e))}finally{setBusy(false)}
  }}>{ru?'Продолжить':'Continue journey'}</button><button disabled={busy} aria-label={ru?'Округа':'District atlas'} onClick={onAtlas}>{ru?'Округа':'Atlas'}</button>
  {onCancel&&waiting&&<button disabled={busy} title={i18n.t('next.cancelTitle')} onClick={async()=>{setBusy(true);setError('');try{await onCancel()}catch(e){setError(i18n.error(e))}finally{setBusy(false)}}}>{ru?'Отменить работу':'Cancel work'}</button>}</div>
  {error&&<p role="alert" className="error">{error}</p>}
 </section>
}
