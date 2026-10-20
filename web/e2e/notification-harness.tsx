import {StrictMode,useState} from 'react'
import {createRoot} from 'react-dom/client'
import {api,ApiError} from '../src/api'
import {NotificationProvider,useNotificationSetting} from '../src/notifications'
import {DEFAULT_NOTIFICATION_PREFERENCES,type GameNotification,type NotificationPreferences,type NotificationPreferenceRequest} from '../src/notificationPolicy'
import {NotificationToggle} from '../src/ui/NotificationToggle'
import {I18nProvider} from '../src/i18n'
import '../src/index.css'

declare global{interface Window{noticeSaves:NotificationPreferenceRequest[];noticeMade:{title:string;body:string;tag:string}[];noticeClosed:number;noticeReads:number;failNoticeSave:'before'|'after'|null;failNoticeRead:boolean;noticePermission:NotificationPermission}}
window.noticeSaves=[];window.noticeMade=[];window.noticeClosed=0;window.noticeReads=0;window.failNoticeSave=null;window.failNoticeRead=false
window.noticePermission=new URLSearchParams(location.search).has('denied')?'denied':'granted'
class FakeNotification {
 static get permission(){return window.noticePermission}
 static async requestPermission(){return window.noticePermission}
 onclick:(()=>void)|null=null
 onclose:(()=>void)|null=null
 constructor(title:string,options?:NotificationOptions){window.noticeMade.push({title,body:options?.body??'',tag:options?.tag??''})}
 close(){window.noticeClosed++;this.onclose?.()}
}
Object.defineProperty(window,'Notification',{value:FakeNotification,configurable:true})
if(new URLSearchParams(location.search).has('background')){
 Object.defineProperty(document,'visibilityState',{get:()=> 'hidden',configurable:true})
 document.hasFocus=()=>false
}
const key=(account:string)=>`notice-harness-server.${account}`
const get=(account:string):NotificationPreferences=>({...JSON.parse(localStorage.getItem(key(account))??JSON.stringify(DEFAULT_NOTIFICATION_PREFERENCES)),serverNow:new Date().toISOString()})
const accountA='11111111-1111-4111-8111-111111111111',accountB='22222222-2222-4222-8222-222222222222'
function Stand(){
 const [account,setAccount]=useState(accountA)
 api.notificationPreferences=async()=>{window.noticeReads++;if(window.failNoticeRead)throw Error('Network unavailable');return get(account)}
 api.saveNotificationPreferences=async body=>{
  window.noticeSaves.push(structuredClone(body))
  if(window.failNoticeSave==='before'){window.failNoticeSave=null;throw Error('Lost response')}
  const receiptKey=`notice-harness-receipt.${account}.${body.requestId}`
  const prior=localStorage.getItem(receiptKey)
  if(prior){if(prior!==JSON.stringify(body))throw new ApiError(400,'Different request','notifications.reusedRequest');return{settings:get(account),replayed:true}}
  const current=get(account)
  if(body.expectedRevision!==current.revision)throw new ApiError(400,'Changed','notifications.changed')
  const updated:NotificationPreferences={...current,...body,revision:current.revision+1,configured:true,consentedAt:body.browserEnabled?new Date().toISOString():current.consentedAt,updatedAt:new Date().toISOString()}
  localStorage.setItem(key(account),JSON.stringify(updated));localStorage.setItem(receiptKey,JSON.stringify(body))
  if(window.failNoticeSave==='after'){window.failNoticeSave=null;throw Error('Lost response')}
  return{settings:get(account),replayed:false}
 }
 return <><button id="switch-account" onClick={()=>setAccount(account===accountA?accountB:accountA)}>Switch account</button><output id="account">{account}</output><NotificationProvider key={account} accountId={account}><Harness account={account}/></NotificationProvider></>
}
function Harness({account}:{account:string}){
 const settings=useNotificationSetting()
 const send=(id:string,category:GameNotification['category'],old=false)=>{
  const now=settings.now(),occurredAt=old?now-600000:now
  settings.emit({id,village:'test-village',category,title:`${category} ${id}`,body:'Fixture only',occurredAt,expiresAt:old?now-1:now+3600000})
 }
 return <div className="modal menu"><div className="modal-body">
  <NotificationToggle/>
  <div className="notification-harness-actions">
   <button id="work-one" onClick={()=>send('one','work')}>Work one</button><button id="work-two" onClick={()=>send('two','work')}>Work two</button>
   <button id="farm" onClick={()=>send('farm-one','farm')}>Farm</button><button id="collect" onClick={()=>settings.cancel('test-village','farm-one')}>Already collected</button>
   <button id="stale" onClick={()=>send('stale','danger',true)}>Expired</button>
   <button id="remote-off" onClick={()=>{const current=get(account);localStorage.setItem(key(account),JSON.stringify({...current,browserEnabled:false,revision:current.revision+1}))}}>Remote revoke</button>
  </div>
 </div></div>
}
createRoot(document.getElementById('app')!).render(<StrictMode><I18nProvider><Stand/></I18nProvider></StrictMode>)
