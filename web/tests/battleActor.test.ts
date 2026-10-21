import test from 'node:test'
import assert from 'node:assert/strict'
import {actorFighter} from '../src/game/battleActor.ts'
import type {RaidFighter} from '../src/api.ts'
const resident={citizenId:'resident',accountId:null,active:true,health:7,maximumHealth:32,mode:'hold'} as RaidFighter
const royal={...resident,isTsar:true,accountId:'owner',citizenId:''} as RaidFighter
test('a political owner still controls its legacy resident body with the actual remaining health',()=>{
 assert.equal(actorFighter([resident],'owner','resident'),resident)
 assert.equal(actorFighter([resident],'owner','resident')?.health,7)
 assert.equal(actorFighter([royal],'owner','resident'),royal)
})
test('missing identity and foreign bodies never match, including a misleading resident AccountId',()=>{
 assert.equal(actorFighter([resident,royal]),undefined)
 assert.equal(actorFighter([resident,royal],'stranger','different'),undefined)
 assert.equal(actorFighter([{...resident,accountId:'owner'}],'owner'),undefined)
})
test('an active legacy body takes priority over an inactive duplicate without manufacturing health',()=>{
 const withdrawn={...royal,active:false,mode:'withdrawn',health:80}
 assert.equal(actorFighter([withdrawn,resident],'owner','resident'),resident)
 assert.equal(actorFighter([resident,royal],'owner','resident'),royal)
 assert.equal(actorFighter([royal,resident],'owner','resident'),royal)
 const fallen={...resident,active:false,health:0}
 assert.equal(actorFighter([fallen],'owner','resident'),fallen)
})
