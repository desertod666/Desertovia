import {createContext,useCallback,useContext,useEffect,useMemo,useRef,useState,type ReactNode} from 'react'
import {api,createOperationId} from './api'
import {appPath} from './paths'
import {useI18n} from './i18n'
import {DEFAULT_NOTIFICATION_PREFERENCES,NotificationDelivery,type GameNotification,type NotificationCategory,type NotificationPreferences,type NotificationPreferenceRequest} from './notificationPolicy'

const deviceKey=(account:string)=>`desertod.notifications.device.${account}`
const seenKey=(account:string)=>`desertod.notifications.seen.${account}`
const fallbackDevices=new Map<string,boolean>()
const supported=()=>typeof window!=='undefined'&&'Notification' in window
function deviceOn(account:string){try{return localStorage.getItem(deviceKey(account))==='on'}catch{return fallbackDevices.get(account)??false}}
function setDevice(account:string,on:boolean){fallbackDevices.set(account,on);try{localStorage.setItem(deviceKey(account),on?'on':'off')}catch{/* Device memory remains usable. */}}
async function deliveryLock<T>(account:string,work:()=>Promise<T>):Promise<T|undefined>{
 if(navigator.locks)return navigator.locks.request(`desertod-notifications-${account}`,{ifAvailable:true},lock=>lock?work():undefined)
 return work()
}
export type NotificationState='unsupported'|'denied'|'off'|'on'
export type NotificationDraft=Omit<NotificationPreferences,'configured'|'consentedAt'|'updatedAt'|'serverNow'>
interface NotificationContextValue {
 settings:NotificationPreferences;ready:boolean;busy:boolean;error:string;state:NotificationState
 refresh:()=>Promise<NotificationPreferences>;save:(draft:NotificationDraft)=>Promise<NotificationPreferences>
 enable:()=>Promise<void>;disable:()=>Promise<void>;emit:(event:GameNotification)=>void;now:()=>number;cancel:(village:string,id:string)=>void
}
const Context=createContext<NotificationContextValue|null>(null)

/** Scope is the authenticated account. Navigation/unmount cancels pending alerts; a new account cannot inherit them. */
export function NotificationProvider({accountId,children}:{accountId:string;children:ReactNode}) {
 const i18n=useI18n(),i18nRef=useRef(i18n);i18nRef.current=i18n
 const [settings,setSettings]=useState<NotificationPreferences>(DEFAULT_NOTIFICATION_PREFERENCES)
 const settingsRef=useRef(settings),active=useRef(true)
 const [ready,setReady]=useState(false),[busy,setBusy]=useState(false),[error,setError]=useState(''),[,changed]=useState(0)
 const [deviceBusy,setDeviceBusy]=useState(false),deviceOperation=useRef(false)
 const inFlight=useRef<Promise<NotificationPreferences>|null>(null),saving=useRef<Promise<NotificationPreferences>|null>(null)
 const requests=useRef(new Map<string,NotificationPreferenceRequest>())
 const deliveryRef=useRef<NotificationDelivery|null>(null)
 const notifications=useRef(new Set<Notification>())
 const apply=useCallback((incoming:NotificationPreferences)=>{
  if(!active.current||incoming.revision<settingsRef.current.revision)return settingsRef.current
  const serverTime=Date.parse(incoming.serverNow??'')
  if(Number.isFinite(serverTime)&&deliveryRef.current)deliveryRef.current.offset=serverTime-Date.now()
  settingsRef.current=incoming;setSettings(incoming);setReady(true);deliveryRef.current?.configure(incoming)
  if(!incoming.browserEnabled){for(const notice of notifications.current)notice.close();notifications.current.clear()}
  return incoming
 },[])
 const refresh=useCallback(()=>{
  if(inFlight.current)return inFlight.current
  const pending=api.notificationPreferences().then(result=>{if(active.current)setError('');return apply(result)},e=>{
   if(active.current)setError(i18nRef.current.error(e));throw e
  }).finally(()=>{if(inFlight.current===pending)inFlight.current=null})
  inFlight.current=pending;return pending
 },[apply])
 const save=useCallback((draft:NotificationDraft)=>{
  if(saving.current)return saving.current
  const {revision,browserEnabled,categories,quietEnabled,quietStartMinute,quietEndMinute,timeZoneId}=draft
  const values={browserEnabled,categories,quietEnabled,quietStartMinute,quietEndMinute,timeZoneId},key=JSON.stringify({...values,revision})
  let body=requests.current.get(key)
  if(!body){body={...values,requestId:createOperationId(),expectedRevision:revision};requests.current.set(key,body)}
  if(requests.current.size>20)requests.current.delete(requests.current.keys().next().value!)
  setBusy(true);setError('')
  const pending=api.saveNotificationPreferences(body).then(result=>{requests.current.delete(key);return apply(result.settings)},e=>{
   if(active.current)setError(i18nRef.current.error(e));throw e
  }).finally(()=>{if(saving.current===pending)saving.current=null;if(active.current)setBusy(false)})
  saving.current=pending;return pending
 },[apply])
 const delivery=useMemo(()=>new NotificationDelivery({
  now:()=>Date.now(),foreground:()=>document.visibilityState==='visible'&&document.hasFocus(),
  deviceAllowed:()=>supported()&&Notification.permission==='granted'&&deviceOn(accountId),
  readSeen:()=>{try{
   const stored:unknown=JSON.parse(localStorage.getItem(seenKey(accountId))??'{}')
   return stored&&typeof stored==='object'&&!Array.isArray(stored)?Object.fromEntries(Object.entries(stored).filter(([key,value])=>key.length<=512&&typeof value==='number'&&Number.isFinite(value))):{}
  }catch{return {}}},
  writeSeen:seen=>{try{localStorage.setItem(seenKey(accountId),JSON.stringify(seen))}catch{/* In-memory dedup still applies. */}},
  freshPreferences:refresh,lock:work=>deliveryLock(accountId,work),
  send:events=>{
   const text=i18nRef.current,first=events[0]!,ru=text.language==='ru'
   const title=events.length===1?first.title:ru?`${events.length} события · ${first.village}`:`${events.length} events · ${first.village}`
   const body=events.length===1?first.body:events.slice(0,3).map(e=>e.title).join('\n')
   const notice=new Notification(title,{body,tag:`desertod:${accountId}:${first.village}:${first.category}`,icon:appPath('/favicon.svg'),badge:appPath('/favicon.svg')})
   notifications.current.add(notice)
   notice.onclick=()=>{window.focus();notice.close()}
   notice.onclose=()=>notifications.current.delete(notice)
  },
 }),[accountId,refresh])
 deliveryRef.current=delivery
 useEffect(()=>{
  active.current=true
  delivery.start()
  void refresh().catch(()=>{})
  const update=()=>{changed(v=>v+1);if(!deviceOn(accountId))delivery.clear();delivery.acknowledgeVisible();if(document.visibilityState==='visible')void refresh().catch(()=>{})}
  window.addEventListener('storage',update);window.addEventListener('focus',update);document.addEventListener('visibilitychange',update)
  return ()=>{active.current=false;delivery.close();for(const notice of notifications.current)notice.close();notifications.current.clear();window.removeEventListener('storage',update);window.removeEventListener('focus',update);document.removeEventListener('visibilitychange',update)}
 },[accountId,delivery,refresh])
 const enable=useCallback(async()=>{
  if(!supported()||busy||deviceOperation.current)return
  deviceOperation.current=true;setDeviceBusy(true)
  setError('')
  try {
   const permission=Notification.permission==='default'?await Notification.requestPermission():Notification.permission
   if(permission!=='granted'){changed(v=>v+1);return}
   const current=await refresh()
   if(!current.browserEnabled)await save({...current,browserEnabled:true,timeZoneId:current.configured?current.timeZoneId:Intl.DateTimeFormat().resolvedOptions().timeZone})
   if(active.current){setDevice(accountId,true);changed(v=>v+1)}
  }catch(e){if(active.current)setError(i18nRef.current.error(e))}
  finally{deviceOperation.current=false;if(active.current)setDeviceBusy(false)}
 },[accountId,busy,refresh,save])
 const disable=useCallback(async()=>{
  if(deviceOperation.current)return
  deviceOperation.current=true;setDeviceBusy(true)
  setDevice(accountId,false);changed(v=>v+1);delivery.clear()
  for(const notice of notifications.current)notice.close();notifications.current.clear()
  try {if(saving.current)await saving.current;const current=await refresh();if(current.browserEnabled)await save({...current,browserEnabled:false})}
  catch(e){if(active.current)setError(i18nRef.current.error(e))}
  finally{deviceOperation.current=false;if(active.current)setDeviceBusy(false)}
 },[accountId,delivery,refresh,save])
 const state:NotificationState=!supported()?'unsupported':Notification.permission==='denied'?'denied':settings.browserEnabled&&deviceOn(accountId)&&Notification.permission==='granted'?'on':'off'
 const value=useMemo(()=>({settings,ready,busy:busy||deviceBusy,error,state,refresh,save,enable,disable,emit:(event:GameNotification)=>delivery.accept(event),now:()=>delivery.currentTime(),cancel:(village:string,id:string)=>delivery.cancel(village,id)}),[settings,ready,busy,deviceBusy,error,state,refresh,save,enable,disable,delivery])
 return <Context.Provider value={value}>{children}</Context.Provider>
}

export function useNotificationSetting(){
 const context=useContext(Context)
 return context??{settings:DEFAULT_NOTIFICATION_PREFERENCES,ready:false,busy:false,error:'',state:'unsupported' as const,
  refresh:async()=>DEFAULT_NOTIFICATION_PREFERENCES,save:async()=>DEFAULT_NOTIFICATION_PREFERENCES,enable:async()=>{},disable:async()=>{},emit:()=>{},now:()=>Date.now(),cancel:()=>{}}
}

export function useGameNotifications(village:string){
 const context=useNotificationSetting()
 const notify=useCallback((title:string,body:string,id:string,category:NotificationCategory,occurredAt:number,expiresAt:number)=>{
  context.emit({title,body,id,category,village,occurredAt,expiresAt})
 },[context,village])
 const cancel=useCallback((id:string)=>context.cancel(village,id),[context,village])
 return {notify,cancel,now:context.now}
}
