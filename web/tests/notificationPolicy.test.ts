import {test} from 'node:test'
import assert from 'node:assert/strict'
import {DEFAULT_NOTIFICATION_PREFERENCES,NotificationDelivery,notificationQuiet,type GameNotification,type NotificationPreferences} from '../src/notificationPolicy.ts'

const base=Date.parse('2026-01-01T12:00:00Z')
function stand(){
 let now=base,foreground=false,allowed=true,fail=false
 let preferences:NotificationPreferences={...DEFAULT_NOTIFICATION_PREFERENCES,browserEnabled:true,configured:true,revision:1}
 let ledger:Record<string,number>={}
 const sent:GameNotification[][]=[]
 const make=()=>new NotificationDelivery({now:()=>now,foreground:()=>foreground,deviceAllowed:()=>allowed,
  readSeen:()=>ledger,writeSeen:value=>{ledger={...value}},freshPreferences:async()=>{if(fail)throw Error('offline');return preferences},
  lock:async work=>work(),send:events=>sent.push(events),schedule:()=>0 as unknown as ReturnType<typeof setTimeout>,cancel:()=>{}})
 const delivery=make();delivery.configure(preferences)
 const event=(id:string,extra:Partial<GameNotification>={}):GameNotification=>({id,village:'v',category:'work',title:'Private title',body:'Private message',occurredAt:now,expiresAt:now+300000,...extra})
 return {delivery,make,sent,event,ledger:()=>ledger,now:(value:number)=>{now=value},foreground:(value:boolean)=>{foreground=value},allowed:(value:boolean)=>{allowed=value},fail:(value:boolean)=>{fail=value},settings:(value:Partial<NotificationPreferences>)=>{preferences={...preferences,...value};return preferences}}
}

test('one batch contains unique events and a reconnect cannot replay them',async()=>{
 const s=stand(),first=s.event('first'),second=s.event('second')
 s.delivery.accept(first);s.delivery.accept(first);s.delivery.accept(second);await s.delivery.flush()
 assert.equal(s.sent.length,1);assert.deepEqual(s.sent[0]!.map(e=>e.id),['first','second'])
 const reconnect=s.make();reconnect.configure(s.settings({}));reconnect.accept(first);reconnect.accept(second);await reconnect.flush()
 assert.equal(s.sent.length,1)
 assert.equal(JSON.stringify(s.ledger()).includes('Private'),false)
})
test('quiet hours cross midnight and use both repeated DST hours correctly',()=>{
 const settings={...DEFAULT_NOTIFICATION_PREFERENCES,quietEnabled:true,timeZoneId:'Europe/Oslo'}
 assert.equal(notificationQuiet(settings,Date.parse('2026-07-01T20:00:00Z')),true)
 assert.equal(notificationQuiet(settings,Date.parse('2026-07-02T06:00:00Z')),false)
 const dst={...settings,quietStartMinute:60,quietEndMinute:180}
 assert.equal(notificationQuiet(dst,Date.parse('2026-10-25T00:30:00Z')),true)
 assert.equal(notificationQuiet(dst,Date.parse('2026-10-25T01:30:00Z')),true)
 assert.equal(notificationQuiet(dst,Date.parse('2026-10-25T02:00:00Z')),false)
 assert.equal(notificationQuiet(dst,Date.parse('2026-03-29T00:59:00Z')),true)
 assert.equal(notificationQuiet(dst,Date.parse('2026-03-29T01:00:00Z')),false)
 assert.equal(notificationQuiet({...settings,timeZoneId:'not-a-zone'},base),true)
})
test('quiet queue releases only fresh notices and cancels already collected crops',async()=>{
 const s=stand();s.now(Date.parse('2026-01-01T21:00:00Z'))
 s.delivery.configure(s.settings({quietEnabled:true,timeZoneId:'Europe/Oslo'}))
 s.delivery.accept(s.event('expired',{category:'farm',expiresAt:Date.parse('2026-01-02T05:00:00Z')}))
 s.delivery.accept(s.event('fresh',{category:'farm',expiresAt:Date.parse('2026-01-02T09:00:00Z')}))
 s.delivery.accept(s.event('collected',{category:'farm',expiresAt:Date.parse('2026-01-02T09:00:00Z')}))
 s.delivery.cancel('v','collected');await s.delivery.flush();assert.equal(s.sent.length,0)
 s.now(Date.parse('2026-01-02T07:00:00Z'));await s.delivery.flush()
 assert.deepEqual(s.sent.flat().map(e=>e.id),['fresh'])
})
test('new consent, current category and device permission are checked at dispatch',async()=>{
 const s=stand();s.delivery.accept(s.event('revoked'));s.settings({browserEnabled:false,revision:2});await s.delivery.flush();assert.equal(s.sent.length,0)
 s.delivery.configure(s.settings({browserEnabled:true,revision:3}));s.delivery.accept(s.event('disabled-category'));s.settings({categories:['farm'],revision:4});await s.delivery.flush();assert.equal(s.sent.length,0)
 s.delivery.configure(s.settings({categories:['work'],revision:5}));s.delivery.accept(s.event('permission-lost'));s.allowed(false);await s.delivery.flush();assert.equal(s.sent.length,0)
})
test('an event read in the foreground stays read across tabs, and focus acknowledges the pending batch',async()=>{
 const s=stand();s.foreground(true);s.delivery.accept(s.event('read'));s.foreground(false)
 const another=s.make();another.configure(s.settings({}));another.accept(s.event('read'));await another.flush();assert.equal(s.sent.length,0)
 s.delivery.accept(s.event('queued'));s.foreground(true);s.delivery.acknowledgeVisible();s.foreground(false);await s.delivery.flush();assert.equal(s.sent.length,0)
})
test('stale and future events cannot be made fresh by replay; a wrong device clock uses server offset',async()=>{
 const s=stand();s.delivery.accept(s.event('expired',{occurredAt:base-600000,expiresAt:base-1}));s.delivery.accept(s.event('future',{occurredAt:base+3600000,expiresAt:base+7200000}));await s.delivery.flush();assert.equal(s.sent.length,0)
 s.delivery.offset=3600000;s.delivery.accept(s.event('correct-server-time',{occurredAt:base+3600000,expiresAt:base+3900000}));await s.delivery.flush();assert.equal(s.sent.length,1)
})
test('offline retries are bounded and closing a page cancels an in-flight authorization',async()=>{
 const s=stand();s.fail(true);s.delivery.accept(s.event('old'))
 await s.delivery.flush();await s.delivery.flush();await s.delivery.flush();s.fail(false);await s.delivery.flush();assert.equal(s.sent.length,0)
 s.delivery.accept(s.event('new'));await s.delivery.flush();assert.deepEqual(s.sent.flat().map(e=>e.id),['new'])
 let resolve!:(p:NotificationPreferences)=>void
 const promise=new Promise<NotificationPreferences>(done=>{resolve=done})
 const pending=new NotificationDelivery({now:()=>base,foreground:()=>false,deviceAllowed:()=>true,readSeen:()=>({}),writeSeen:()=>{},freshPreferences:()=>promise,lock:async work=>work(),send:()=>assert.fail('Old account delivered after unmount'),schedule:()=>0 as unknown as ReturnType<typeof setTimeout>,cancel:()=>{}})
 pending.configure({...DEFAULT_NOTIFICATION_PREFERENCES,browserEnabled:true});pending.accept(s.event('switch-account'));const task=pending.flush();pending.close();resolve({...DEFAULT_NOTIFICATION_PREFERENCES,browserEnabled:true});await task
})
test('queues and identity storage remain bounded during a large reconnect batch',async()=>{
 const s=stand();for(let i=0;i<1000;i++)s.delivery.accept(s.event(String(i)))
 await s.delivery.flush();assert.equal(s.sent.flat().length,100);assert.ok(Object.keys(s.ledger()).length<=500)
 for(let round=0;round<8;round++){for(let i=0;i<100;i++)s.delivery.accept(s.event(`round${round}-${i}`));await s.delivery.flush()}
 assert.ok(Object.keys(s.ledger()).length<=500)
})
