import test from 'node:test'
import assert from 'node:assert/strict'
import {journeyPosition} from '../src/game/journeyPosition.ts'
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
