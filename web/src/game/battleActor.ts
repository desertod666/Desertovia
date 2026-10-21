import type {RaidFighter} from '../api'

/** Political role never hides the account's already admitted physical fighter. */
export function actorFighter(fighters:readonly RaidFighter[],accountId?:string|null,citizenId?:string|null):RaidFighter|undefined {
  const own=fighters.filter(f=>f.isTsar?!!accountId&&f.accountId===accountId:!!citizenId&&f.citizenId===citizenId)
  const active=own.filter(f=>f.active&&f.health>0&&!f.wounded&&f.mode!=='withdrawn')
  return active.find(f=>f.isTsar)??active[0]??own.find(f=>f.isTsar)??own[0]
}
