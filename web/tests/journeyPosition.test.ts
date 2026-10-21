import test from 'node:test'
import assert from 'node:assert/strict'
import {journeyPosition,receiveJourney} from '../src/game/journeyPosition.ts'
const trip={from:{x:0,y:0},waypoints:[{x:10,y:0,cost:1},{x:10,y:10,cost:2.5}],setOutAt:'2026-01-01T12:00:00Z',arrivesAt:'2026-01-01T12:00:21Z',speedMultiplier:1,snapshotAt:'2026-01-01T12:00:00Z'}
test('persisted turns are interpolated with marsh time, not a diagonal shortcut',()=>{
 assert.deepEqual(journeyPosition(trip,3000),{x:5,y:0,moving:true})
 assert.deepEqual(journeyPosition(trip,13500),{x:10,y:5,moving:true})
 assert.deepEqual(journeyPosition(trip,21000),{x:10,y:10,moving:false})
})
test('a reloaded snapshot resumes the same point using server time rather than device wall clock',()=>{
 const reload={...trip,snapshotAt:'2026-01-01T12:00:10Z'}
 assert.deepEqual(journeyPosition(reload,3500),journeyPosition(trip,13500))
 assert.deepEqual(journeyPosition(reload,60000),{x:10,y:10,moving:false})
})
test('medical speed and a future start preserve one continuous saved route',()=>{
 assert.deepEqual(journeyPosition({...trip,speedMultiplier:.5},6000),{x:5,y:0,moving:true})
 assert.deepEqual(journeyPosition({...trip,snapshotAt:'2026-01-01T11:59:59Z'},0),{x:0,y:0,moving:false})
})
test('a new obstruction ahead stops at the saved point rather than the old arrival',()=>{
 const blocked={...trip,blockedAt:'2026-01-01T12:00:03Z',blockReason:'gate'}
 assert.deepEqual(journeyPosition(blocked,1500),{x:2.5,y:0,moving:true})
 assert.deepEqual(journeyPosition(blocked,3000),{x:5,y:0,moving:false})
 assert.deepEqual(journeyPosition(blocked,60000),{x:5,y:0,moving:false})
})
test('reloading after an obstruction retains its location even with a later server snapshot',()=>{
 const blocked={...trip,blockedAt:'2026-01-01T12:00:13.500Z',snapshotAt:'2026-01-01T12:01:00Z'}
 assert.deepEqual(journeyPosition(blocked,0),{x:10,y:5,moving:false})
 assert.deepEqual(journeyPosition(blocked,60000),{x:10,y:5,moving:false})
})

test('duplicate world/title/citizen refreshes preserve the original receipt clock while walking',()=>{
 let sample=receiveJourney(null,trip,1000)!
 const original=sample
 for(let at=1500;at<=4000;at+=500)sample=receiveJourney(sample,structuredClone(trip),at)!
 assert.equal(sample,original)
 assert.deepEqual(journeyPosition(sample.value,4000-sample.receivedAt),{x:5,y:0,moving:true})
})

test('an explicit resume of the same journey replaces its route and starts the new snapshot clock',()=>{
 const old=receiveJourney(null,{...trip,id:'same',blockedAt:'2026-01-01T12:00:03Z'},1000)!
 const next={...trip,id:'same',from:{x:5,y:0},waypoints:[{x:5,y:10,cost:1}],setOutAt:'2026-01-01T12:01:00Z',arrivesAt:'2026-01-01T12:01:06Z',snapshotAt:'2026-01-01T12:01:00Z',blockedAt:null}
 const resumed=receiveJourney(old,next,6000)!
 assert.notEqual(resumed,old);assert.equal(resumed.receivedAt,6000)
 assert.deepEqual(journeyPosition(resumed.value,600),{x:5,y:1,moving:true})
 assert.equal(receiveJourney(resumed,old.value,7000),resumed)
})

test('a blocked return with only the occupied point never invents a route home',()=>{
 const exit={x:-43.5,y:13.5},waiting={...trip,from:exit,waypoints:[{...exit,cost:1}],arrivesAt:trip.setOutAt,blockedAt:trip.setOutAt}
 let sample=receiveJourney(null,waiting,1000)!
 sample=receiveJourney(sample,structuredClone(waiting),90000)!
 assert.deepEqual(journeyPosition(sample.value,3600000),{...exit,moving:false})
 assert.equal(receiveJourney(sample,null,90001),null)
})
