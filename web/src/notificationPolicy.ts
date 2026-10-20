export const NOTIFICATION_CATEGORIES=['work','building','farm','adventure','danger','civic'] as const
export type NotificationCategory=typeof NOTIFICATION_CATEGORIES[number]
export interface NotificationPreferences {
 serverNow?:string
 revision:number;configured:boolean;browserEnabled:boolean;categories:NotificationCategory[]
 quietEnabled:boolean;quietStartMinute:number;quietEndMinute:number;timeZoneId:string;consentedAt:string|null;updatedAt:string|null
}
export interface NotificationPreferenceRequest extends Omit<NotificationPreferences,'revision'|'configured'|'consentedAt'|'updatedAt'> {requestId:string;expectedRevision:number}
export interface GameNotification {id:string;village:string;category:NotificationCategory;title:string;body:string;occurredAt:number;expiresAt:number}
export const DEFAULT_NOTIFICATION_PREFERENCES:NotificationPreferences={revision:0,configured:false,browserEnabled:false,categories:[...NOTIFICATION_CATEGORIES],quietEnabled:false,quietStartMinute:1320,quietEndMinute:480,timeZoneId:'UTC',consentedAt:null,updatedAt:null}

export function notificationQuiet(settings:NotificationPreferences,now:number):boolean {
 if(!settings.quietEnabled)return false
 try {
  const parts=new Intl.DateTimeFormat('en-GB',{timeZone:settings.timeZoneId,hour:'2-digit',minute:'2-digit',hourCycle:'h23'}).formatToParts(now)
  const minute=Number(parts.find(p=>p.type==='hour')!.value)*60+Number(parts.find(p=>p.type==='minute')!.value)
  if(settings.quietStartMinute===settings.quietEndMinute)return true // Invalid stored settings fail closed.
  return settings.quietStartMinute<settings.quietEndMinute?minute>=settings.quietStartMinute&&minute<settings.quietEndMinute:minute>=settings.quietStartMinute||minute<settings.quietEndMinute
 }catch{return true}
}

interface DeliveryEnvironment {
 now:()=>number;foreground:()=>boolean;deviceAllowed:()=>boolean
 readSeen:()=>Record<string,number>;writeSeen:(seen:Record<string,number>)=>void
 freshPreferences:()=>Promise<NotificationPreferences>
 lock:<T>(work:()=>Promise<T>)=>Promise<T|undefined>
 send:(events:GameNotification[])=>void
 schedule?:(work:()=>void,delay:number)=>ReturnType<typeof setTimeout>
 cancel?:(timer:ReturnType<typeof setTimeout>)=>void
}

/** Only bounded event identities are persisted. Titles, messages and gameplay state stay out of browser storage. */
export class NotificationDelivery {
 private pending=new Map<string,GameNotification>()
 private memorySeen:Record<string,number>={}
 private timer:ReturnType<typeof setTimeout>|undefined
 private stopped=false
 private configured=false
 private flushing=false
 private failures=0
 private preferences:NotificationPreferences=DEFAULT_NOTIFICATION_PREFERENCES
 offset=0
 private env:DeliveryEnvironment
 constructor(env:DeliveryEnvironment){this.env=env}
 private now(){return this.env.now()+this.offset}
 private key(event:GameNotification){return `${event.village}:${event.id}`}
 private seen(){const now=this.now();return Object.fromEntries(Object.entries({...this.env.readSeen(),...this.memorySeen}).filter(([,until])=>until>now).sort((a,b)=>b[1]-a[1]).slice(0,500))}
 private remember(events:GameNotification[]){
  for(const event of events)this.memorySeen[this.key(event)]=Math.min(event.expiresAt+86400000,this.now()+7*86400000)
  this.memorySeen=this.seen();this.env.writeSeen(this.memorySeen)
 }
 configure(preferences:NotificationPreferences){
  if(preferences.revision<this.preferences.revision)return
  this.configured=true
  this.preferences=preferences
  if(!preferences.browserEnabled){this.clear();return}
  for(const [key,event] of this.pending)if(!preferences.categories.includes(event.category))this.pending.delete(key)
 }
 accept(event:GameNotification){
  const now=this.now(),key=this.key(event)
  if(this.stopped||!event.id||!event.village||!Number.isFinite(event.occurredAt)||!Number.isFinite(event.expiresAt)
   ||event.expiresAt<=now||event.occurredAt>now+60000||event.expiresAt<=event.occurredAt
   ||this.configured&&!this.preferences.browserEnabled||!this.preferences.categories.includes(event.category)||!this.env.deviceAllowed())return
  if(this.seen()[key]||this.pending.has(key))return
  if(this.env.foreground()){
   // Reading the event in the active game also suppresses a replay in another tab.
   this.memorySeen[key]=Math.min(event.expiresAt+86400000,now+7*86400000)
   void this.env.lock(async()=>{if(!this.stopped)this.remember([event])}).catch(()=>{})
   return
  }
  if(this.pending.size>=100)this.pending.delete(this.pending.keys().next().value!)
  this.pending.set(key,event);this.schedule(event.category==='danger'?0:3000)
 }
 private schedule(delay:number){
  if(this.timer!==undefined||this.stopped)return
  this.timer=(this.env.schedule??setTimeout)(()=>{this.timer=undefined;void this.flush()},delay)
 }
 async flush(){
  if(this.stopped||this.flushing)return
  this.flushing=true
  try {
   for(const [key,event] of this.pending)if(event.expiresAt<=this.now())this.pending.delete(key)
   if(!this.pending.size)return
   if(!this.env.deviceAllowed()){this.clear();return}
   // A cache is insufficient after consent was revoked on another device. No network => no alert.
   const latest=await this.env.freshPreferences()
   if(this.stopped)return
   this.configure(latest)
   if(!latest.browserEnabled||!this.pending.size)return
   if(notificationQuiet(latest,this.now())){this.schedule(30000);return}
   await this.env.lock(async()=>{
    if(this.stopped)return
    const seen=this.seen(),fresh=[...this.pending.values()].filter(e=>e.expiresAt>this.now()&&!seen[this.key(e)]&&latest.categories.includes(e.category))
    if(this.env.foreground())this.remember(fresh)
    else {
     const groups=new Map<string,GameNotification[]>()
     for(const event of fresh){const key=`${event.village}:${event.category}`;groups.set(key,[...(groups.get(key)??[]),event])}
     for(const events of groups.values()){
      if(this.stopped||!this.env.deviceAllowed())return
      // Persist the attempt before the external browser call; a process crash cannot replay the attempt.
      this.remember(events)
      this.env.send(events)
     }
    }
    for(const event of fresh)this.pending.delete(this.key(event))
    for(const key of this.pending.keys())if(seen[key])this.pending.delete(key)
   })
   this.failures=0
  }catch {
   // Settings/permission failures never repeat a game action. Keep only a short, bounded retry window.
   this.failures++
   if(this.failures>=3)this.clear()
  }finally {
   this.flushing=false
   if(this.pending.size)this.schedule(this.failures?10000:3000)
  }
 }
 clear(){if(this.timer!==undefined)(this.env.cancel??clearTimeout)(this.timer);this.timer=undefined;this.pending.clear()}
 cancel(village:string,id:string){this.pending.delete(`${village}:${id}`)}
 acknowledgeVisible(){
  if(!this.env.foreground())return
  const events=[...this.pending.values()];this.pending.clear()
  for(const event of events)this.memorySeen[this.key(event)]=Math.min(event.expiresAt+86400000,this.now()+7*86400000)
  void this.env.lock(async()=>{if(!this.stopped)this.remember(events)}).catch(()=>{})
 }
 start(){this.stopped=false}
 currentTime(){return this.now()}
 close(){this.stopped=true;this.clear()}
}
