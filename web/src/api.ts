import { appPath, appBase } from './paths'
export type Stat = 'Strength' | 'Dexterity' | 'Intelligence' | 'Charisma' | 'Endurance'

export const STATS: Stat[] = ['Strength', 'Dexterity', 'Intelligence', 'Charisma', 'Endurance']

/** Crafts a citizen gets better at by doing them; every task, construction job and research trains one. */
export type Skill =
  | 'Woodcutting'
  | 'Quarrying'
  | 'Gathering'
  | 'Hunting'
  | 'Fishing'
  | 'Farming'
  | 'Mining'
  | 'Smithing'
  | 'Carpentry'
  | 'Baking'
  | 'Scholarship'
  | 'Building'
  | 'Hauling'
  | 'Entertaining'
  | 'Exploring'
  | 'Trading'
  | 'Guarding'
  | 'Weatherlore'
  | 'Weaponcraft'
  | 'Recovery'

export type WorkState = 'Idle' | 'Working' | 'AwaitingRenewal' | 'Paused'

export type RoleTier = 'Basic' | 'Journeyman' | 'Master'

export type TaskEffect =
  | 'Resources'
  | 'Morale'
  | 'Deliver'
  | 'Survey'
  | 'Build'
  | 'Supervise'
  | 'Expedition'
  | 'Roam'
  | 'Unload'
  | 'Sow'
  | 'Water'
  | 'Harvest'
  | 'Guard'
  | 'Repair'
  | 'NotAvailableYet'
  | 'Sharpen'

export type ProjectPhase = 'Survey' | 'Construction' | 'Completed'

export interface StatRequirement {
  stat: Stat
  minimum: number
}

/** A profession: a title earned by a level in a craft. It does not gate tasks; each task needs its own craft level. */
export interface Role {
  id: string
  name: string
  tier: RoleTier
  /** Craft of the profession; null for laborers, which everyone is. */
  skill: Skill | null
  /** Level of `skill` that earns the title. */
  skillLevel: number
  /** Technology the village must discover before anyone works in this role. */
  technology: string | null
}

/** A craft and the stats it rests on: level L needs the primary stat at L − 1 and the secondary at (L − 1) / 2. */
export interface SkillDefinition {
  kind: Skill
  primary: Stat
  secondary: Stat
  /** Stats needed for every level, from 1 to the maximum. */
  levels: { level: number; stats: StatRequirement[] }[]
}

export interface ResourceAmount {
  resource: string
  amount: number
  /** Edge ground at the smithy. Absent or 0 is an ordinary weapon. */
  sharpen?: number
}

export interface Task {
  id: string
  name: string
  roleId: string
  /** Level of `skill` needed to do the task; levels above it yield more and take less time, far above it teach little. */
  skillLevel: number
  primary: Stat
  secondary: Stat
  /** Craft the task trains. */
  skill: Skill
  effect: TaskEffect
  baseYield: number
  outputs: ResourceAmount[]
  inputs: ResourceAmount[]
  workplace: string
  /** The task can be done only once the village has this building. */
  requiredBuilding: BuildingKind | null
  /** Project the village must have finished first. */
  requiredProject: string | null
  /** Technology the village must have discovered first. */
  requiredTechnology: string | null
  workSeconds: number
  /** Set for hunting: the catch comes out of the village's herd of this animal. */
  hunt: { prey: AnimalKind; animals: number } | null
  /** Set for sowing: the crop put in the bed. */
  crop: CropKind | null
  /** Set for work out in the wild, where beasts may attack: chance per cycle in daylight, and who attacks. */
  danger: { chance: number; enemies: EnemyKind[] } | null
}

/** Citizens take weapons from storage for these tasks. */
export const isArmed = (task: Task) => task.danger !== null || task.effect === 'Guard'

export type AnimalKind = 'Rabbit' | 'Deer' | 'Bird'

export type EnemyKind = 'Wolf' | 'Boar' | 'Bear' | 'Zombie' | 'Skeleton' | 'Rat' | 'Imp' | 'Ghoul' | 'Wraith' | 'BarrowKing'

/** The undead the Tsar can call onto the camp; the others live only in dungeons. */
export const UNDEAD: EnemyKind[] = ['Zombie', 'Skeleton']

export type RaidSize = 'Small' | 'Medium' | 'Large'

export const RAID_SIZES: RaidSize[] = ['Small', 'Medium', 'Large']

export interface EnemyDefinition {
  kind: EnemyKind
  name: string
  power: number
  ambushWeight: number
  /** Goods one slain enemy leaves. */
  loot: ResourceAmount[]
  /** Goods one raider gets away with when the village loses. */
  steals: ResourceAmount[]
  /** Damage one raider does to a building when the village loses. */
  damage: number
  injuryMinutes: number
  undead: boolean
  /** Lives only in dungeons: never raids the camp. */
  dungeonOnly: boolean
}

export type HeroClass = 'Brawler' | 'Spearman' | 'Swordsman' | 'Archer' | 'Shieldbearer' | 'Healer' | 'Voivode'

export const HERO_CLASSES: HeroClass[] = ['Brawler', 'Spearman', 'Swordsman', 'Archer', 'Shieldbearer', 'Healer', 'Voivode']

export interface HeroClassDefinition {
  class: HeroClass
  /** Taken from storage for the run. */
  weapon: string | null
  armor: string | null
  /** Craft the class needs, at `skillLevel`. */
  skill: Skill | null
  skillLevel: number
  stat: Stat
  /** Reach of an attack, in tiles. */
  range: number
  attackSeconds: number
  healthMultiplier: number
  damageMultiplier: number
  /** Health given back per use; 0 when the class cannot heal. */
  heal: number
}

export interface DungeonLoot {
  resource: string
  minimum: number
  maximum: number
  chance: number
}

export interface DungeonFoe {
  kind: EnemyKind
  health: number
  damage: number
  attackSeconds: number
  speed: number
  aggroRange: number
  attackRange: number
  experience: number
  loot: DungeonLoot[]
}

export interface DungeonInfo {
  id: string
  name: string
  width: number
  height: number
  timeLimitSeconds: number
  foes: number
}

export const DEAD_LANDS = 'dead-lands'

export type DungeonPhase = 'Gathering' | 'Marching' | 'Running' | 'Ended'
export type DungeonOutcome = 'Pending' | 'Victory' | 'Defeat' | 'Abandoned' | 'Cancelled'

export interface DungeonHero {
  /** Null for the Tsar. */
  citizenId: string | null
  name: string
  class: HeroClass
  /** Reaches the edge of the land then. */
  arrivesAt: string | null
  knockedOut: boolean
  killer: EnemyKind | null
  /** Back in the village with rewards, wound and gear sorted out. */
  settled: boolean
  kills: number
  experience: number
  loot: ResourceAmount[]
  broken: string[]
}

/** The village's dungeon run: open, under way, or ended a moment ago. */
export interface DungeonRun {
  id: string
  dungeonId: string
  phase: DungeonPhase
  outcome: DungeonOutcome
  openedAt: string
  gatheringEndsAt: string
  marchEndsAt: string | null
  startedAt: string | null
  endedAt: string | null
  slain: number
  foes: number
  heroes: DungeonHero[]
}

export interface HeroClassOffer {
  class: HeroClass
  health: number
  damage: number
  range: number
  weapon: string | null
  armor: string | null
}

export type HeroState = 'Fighting' | 'Fleeing' | 'Left'

export interface DungeonRunHero {
  /** The citizen, or the Tsar's account. */
  id: string
  citizenId: string | null
  name: string
  class: HeroClass
  appearance: Appearance | null
  maximumHealth: number
}

export type DungeonEventKind = 'Hit' | 'Struck' | 'Healed' | 'Slain' | 'KnockedOut' | 'Left'

export interface DungeonEvent {
  kind: DungeonEventKind
  heroId: string | null
  foeId: number | null
  amount: number
  /** An arrow or a bolt flew. */
  ranged: boolean
}

export interface DungeonHeroSnapshot {
  id: string
  x: number
  y: number
  health: number
  state: HeroState
  targetFoe: number | null
  healTarget: string | null
  arrows: number | null
  kills: number
  experience: number
  loot: ResourceAmount[]
}

export interface DungeonFoeSnapshot {
  id: number
  kind: EnemyKind
  x: number
  y: number
  health: number
  maximumHealth: number
  target: string | null
}

export interface DungeonSnapshot {
  runId: string
  elapsed: number
  outcome: DungeonOutcome
  heroes: DungeonHeroSnapshot[]
  foes: DungeonFoeSnapshot[]
  events: DungeonEvent[]
  /** Zero on the first floor. */
  floor: number
  floorCount: number
  width: number
  height: number
  tiles: string
  entrance: TilePoint
}

/** Tiles of a dungeon map, by the digit the server sends for each. */
export type DungeonTile = 'Ground' | 'DeadGrass' | 'Bones' | 'Entrance' | 'DeadTree' | 'Grave' | 'Fence' | 'Mound' | 'Rock'

export const DUNGEON_TILES: DungeonTile[] = ['Ground', 'DeadGrass', 'Bones', 'Entrance', 'DeadTree', 'Grave', 'Fence', 'Mound', 'Rock']

export interface DungeonRunView {
  runId: string
  slug: string
  dungeonId: string
  width: number
  height: number
  /** One digit per tile, row by row from the top-left: an index into `DUNGEON_TILES`. */
  tiles: string
  entrance: TilePoint
  timeLimitSeconds: number
  heroes: DungeonRunHero[]
  /** The viewer's own hero, if they fight in this run. */
  myHeroId: string | null
  run: DungeonRun
  /** Null while the run has not started or once it has ended. */
  snapshot: DungeonSnapshot | null
}

/** The page of a run; the village page sends each hero there once they reach the edge of the land. */
export const dungeonPath = (slug: string, runId: string) => `/v/${encodeURIComponent(slug)}/dungeon/${runId}`

/** The player's own hero in the run: the Tsar's for the owner, else their citizen's. */
export const myHero = (run: DungeonRun, citizenId: string | null, isOwner: boolean) =>
  run.heroes.find((h) => (isOwner ? h.citizenId === null : citizenId !== null && h.citizenId === citizenId)) ?? null

/** When the player's own figure reaches the edge of the land (the fight may start sooner), or null if they do not march. */
export function myArrival(run: DungeonRun, citizenId: string | null, isOwner: boolean) {
  const arrivesAt = myHero(run, citizenId, isOwner)?.arrivesAt
  if (!arrivesAt) return null
  const arrives = Date.parse(arrivesAt)
  return run.marchEndsAt ? Math.min(arrives, Date.parse(run.marchEndsAt)) : arrives
}

export interface WeaponDefinition {
  kind: string
  power: number
  breakChance: number
  /** Arrows shot in one fight; the weapon is useless without them. */
  arrows: number
}

export interface ArmorDefinition {
  kind: string
  /** Chance that a blow that would wound the wearer is stopped. */
  protection: number
  breakChance: number
}

export type RaidSource = 'Night' | 'Undead'
export type RaidOutcome = 'Pending' | 'Repelled' | 'Lost' | 'Cancelled' | 'PropertyDamaged'

/** An attack on the way, or a fought one kept for the record. */
export interface FortificationQuote {level:number;nextLevel:number|null;sections:number;strength:number;damaged:number;work:number;materials:ResourceAmount[];blockedBy:string|null;readyAfterBattle:boolean}
export interface Fortification { id:string; x:number; y:number; gate:boolean; open:boolean; damage:number; level?:number }
export interface RaidUnit {
  enemy?:EnemyKind|null; id:string; variant:'weak'|'regular'|'strong'|'boss'; x:number; y:number; health:number; maximumHealth:number;
  attack:number; speed:number; range:number; state:string; targetKind:string|null; targetId:string|null;
  diedAt:string|null; exitedAt:string|null; nextAttackAt?:string;
  bossPhase?:number|null; strikeX?:number|null; strikeY?:number|null; strikeAt?:string|null;
}
export interface RaidSettings {
  preset:'boss'|'mixed'|'swarm'|'elite'; mode:'automatic'|'manual'; enemy:EnemyKind; sector:string|null;
  count:number; waves:number; warningSeconds:number; waveIntervalSeconds:number; durationSeconds:number;
  budgetMultiplier:number; healthMultiplier:number; attackMultiplier:number; propertyMultiplier:number; speedMultiplier:number;
  weakPercent:number; elitePercent:number; tactic:string; retreatSurvivorFraction:number;
  propertyLimit:number; wallLimit:number; lootLimit:number; theftFraction:number;
  bossStrike:boolean; bossWarningSeconds:number; bossSecondWarningSeconds:number; bossCooldownSeconds:number;
  bossSecondCooldownSeconds:number; bossSecondPhase:boolean; species:{enemy:EnemyKind;percent:number}[];
}
export interface RaidPreview {
  recommendedBudget:number; actualCost:number; count:number; totalHealth:number; totalAttack:number;
  risk:string; units:{enemy:EnemyKind;variant:string;health:number;attack:number;speed:number;cost:number}[];
}
export interface SavedRaidPreset {id:string;name:string;updatedAt:string;settings:RaidSettings}
export interface RaidEditorView {presets:SavedRaidPreset[];coefficient:number;reason:string}

export interface RaidFighter {
  isTsar?:boolean; accountId?:string|null;
  citizenId:string; x:number; y:number; health:number; maximumHealth:number; damageDealt:number; kills:number;
  weapon?:string|null; armor?:string|null; arrows?:number; renownEarned?:number; nextAttackAt?:string; wounded:boolean; guard:boolean; active:boolean; mode:'hold'|'intercept'|'retreating'|'withdrawn'; healReadyAt?:string|null; targetUnitId:string|null;
}
export interface RaidBattle {
  id:string; villageId:string; phase:string; sector:string; tactic:string; composition:string;
  threatBudget:number; readyDefenders:number; availableReserves:number; startsAt:string; lastTickAt:string;
  endsAt:string; finishedAt:string|null; revision:number; deferred:boolean;
  settings?:RaidSettings|null; eventId?:string|null; result?:string|null; playerHealthLost?:number; remediesUsed?:number;
  units:RaidUnit[]; fighters:RaidFighter[]; damage?:{plotId:string;amount:number}[];
  events:{sequence:number;kind:string;target:string;at:string}[];
}

/** Citizens still drawn in a live fight, including ones who closed the game after joining. */
export function fightingCitizenIds(battles: readonly RaidBattle[] | undefined): Set<string> {
  const ids = new Set<string>()
  for (const battle of battles ?? []) {
    if (battle.finishedAt || battle.phase === 'Warning' || battle.phase === 'Cancelled') continue
    for (const fighter of battle.fighters) {
      if (fighter.isTsar || !fighter.citizenId || fighter.health <= 0) continue
      if (fighter.active || fighter.mode === 'withdrawn') ids.add(fighter.citizenId)
    }
  }
  return ids
}

/** Keep authoritative actor revisions when a slow snapshot races a newer combat delta. */
export function mergeBattles(previous:readonly RaidBattle[] = [], incoming:readonly RaidBattle[] = []):RaidBattle[] {
  const all=new Map(previous.map(b=>[b.id,b]));
  for(const next of incoming)if(next.revision>=(all.get(next.id)?.revision??-1))all.set(next.id,next);
  return [...all.values()].sort((a,b)=>Date.parse(a.startsAt)-Date.parse(b.startsAt)).slice(-16);
}

/**
 * A full village snapshot lists every battle the server still publishes.
 * Keep newer revisions, including a battle newer than the snapshot itself; prune older unpublished fights.
 */
export function adoptVillageState(previous: VillageState, next: VillageState): VillageState {
  if (previous.id !== next.id) return next
  if (!next.battles) return { ...next, battles: previous.battles }
  const published = new Set(next.battles.map(battle => battle.id))
  const snapshotAt = Date.parse(next.snapshotAt ?? '')
  // Снимок мог быть прочитан до появления боя, а прийти после его первого delta.
  const kept = (previous.battles ?? []).filter(battle => published.has(battle.id)
    || !battle.finishedAt && Date.parse(battle.lastTickAt) > snapshotAt)
  const battles = mergeBattles(kept, next.battles)
  const tick = (list: readonly RaidBattle[]) => Math.max(0, ...list.map(battle => Date.parse(battle.lastTickAt)))
  const newer = kept.filter(battle => battle.revision > (next.battles?.find(item => item.id === battle.id)?.revision ?? -1))
  return {
    ...next,
    battles,
    fortifications: newer.length > 0 && tick(newer) > tick(next.battles) ? previous.fortifications : next.fortifications,
  }
}

export interface Raid {
  summary?:{contactSeconds:number;participants:number;playerHealthLost:number;remediesUsed:number;arrowsSpent:number;brokenItems:number;propertyDamage:number;wallDamage:number;nextCoefficient:number;reason:string}|null
  engineVersion?:number
  id: string
  source: RaidSource
  enemy: EnemyKind
  count: number
  wave: number
  waves: number
  /** The attack is shown from then on. */
  announcedAt: string
  strikesAt: string
  outcome: RaidOutcome
  resolvedAt: string | null
  killed: number
  defenders: number
  injured: number
  damagedBuildings: number
  stolen: ResourceAmount[]
  loot: ResourceAmount[]
  entrySector?: string | null
  approachPath?: { x: number; y: number }[] | null
}

export interface FightReport {
  enemy: EnemyKind
  won: boolean
  injured: boolean
  recovery?: ExternalRecovery | null
  injuredUntil: string | null
  power: number
  enemyPower: number
  weapon: string | null
  armor: string | null
  loot: ResourceAmount[]
  broken: string[]
}

export interface RaidReport {
  outcome?:RaidOutcome|null
  engineVersion?:number
  raidId: string
  source: RaidSource
  enemy: EnemyKind
  count: number
  wave: number
  waves: number
  strikesAt: string
  repelled: boolean
  killed: number
  defense: number
  attack: number
  defenders: Array<{ citizenId: string; name: string; injured: boolean; renown: number; broken: string[] }>
  victims: Array<{ citizenId: string; name: string }>
  loot: ResourceAmount[]
  stolen: ResourceAmount[]
  damagedPlots: string[]
  moraleChange: number
}

/** Attacks the players can see coming now, soonest first. */
export function comingRaids(raids: Raid[], now: number) {
  return raids
    .filter((raid) => raid.outcome === 'Pending' && Date.parse(raid.announcedAt) <= now)
    .sort((a, b) => Date.parse(a.strikesAt) - Date.parse(b.strikesAt))
}

/** True while the citizen cannot work because of a wound. */
export const isInjured = (citizen: { injuredUntil: string | null }, now: number) =>
  citizen.injuredUntil !== null && Date.parse(citizen.injuredUntil) > now

export type CropKind = 'Turnip' | 'Carrot' | 'Cabbage' | 'Flax' | 'Rye'

export interface CropDefinition {
  kind: CropKind
  name: string
  /** Farming level needed to sow it. */
  skillLevel: number
  /** Time to ripen in a bed that is kept watered; a dry bed grows slower. */
  growHours: number
  /** Goods of a ripe bed at a stat factor of 1. */
  harvest: ResourceAmount[]
}

/** One bed of a farm. */
export interface FieldBed {
  /** Plot of the farm. */
  plotId: string
  number?: number
  displayNumber?: number
  index: number
  /** Null while the bed is empty. */
  crop: CropKind | null
  /** 0 when sown, 1 when ripe. */
  growth: number
  /** The bed is wet until then. */
  wateredUntil: string | null
  /** Growing and drying out soon: watering reaches it. */
  thirsty: boolean
}

/** Mirrors the server's tolerance for a ripe bed. */
const RIPE = 1 - 1e-6

export const isRipe = (bed: FieldBed) => bed.crop !== null && bed.growth >= RIPE

/** Why farm work cannot start now, mirroring the server's checks; null when it can. */
export function fieldsBlock(task: Task, fields: FieldBed[]): 'farm.noEmptyBed' | 'farm.nothingToWater' | 'farm.nothingRipe' | null {
  switch (task.effect) {
    case 'Sow':
      return fields.some((bed) => bed.crop === null) ? null : 'farm.noEmptyBed'
    case 'Water':
      return fields.some((bed) => bed.thirsty) ? null : 'farm.nothingToWater'
    case 'Harvest':
      return fields.some(isRipe) ? null : 'farm.nothingRipe'
    default:
      return null
  }
}

/** Game animals on the village land. The Tsar sees them on the map, never the count. */
export interface Herd {
  kind: AnimalKind
  count: number
  capacity: number
  /** Workplace where they live and are hunted. */
  habitat: string
}

export interface Rules {
  cycleSeconds: number
  graceSeconds: number
  statMaximum: number
  talentCap: number
  secondsPerTile: number
  maximumOpenSites: number
  cyclesPerMeal: number
  /** Most of one good a citizen may hold after taking it out of storage. */
  takeLimit: number
  /** Each repeat of a building needs this much more material than the previous one. */
  repeatCostGrowth: number
  skillMaximum: number
  /** Skill levels above what a task needs make it yield more and take less time, up to this many levels. */
  skillBonusLevels: number
  skillYieldPerLevel: number
  skillWorkTimePerLevel: number
  /** Energy one vigor drink gives back. */
  vigorDrinkEnergy: number
  /** How far, in tiles, a walker sees around them through the fog of war. */
  sightRadius: number
  /** Ambushes are this much more likely at night. */
  nightAmbushMultiplier: number
  /** Damage at which a building works at half speed. */
  maximumDamage: number
  /** Maximum energy is `energyBase + energyPerEndurance × Endurance`. */
  energyBase: number
  energyPerEndurance: number
  /** Hours to refill energy from 0 at full rest speed. */
  energyFullRegenerationHours: number
  energyCostPerCycle: number
  /** Yield of a task done with no energy left; it gives no experience. */
  tiredYieldMultiplier: number
  hungryExperienceMultiplier: number
  hungryRegenerationMultiplier: number
  /** Rest speed without a bed. */
  underTreesRegeneration: number
  /** Extra rest multiplier under the trees while it rains. */
  rainUnderTreesRegeneration: number
  streakPerRenewal: number
  streakMaximumBonus: number
  moraleMinimum: number
  moraleMaximum: number
}

export type BuildingKind = string

export interface JobDefinition {
  id: string
  name: string
  kind: 'Haul' | 'Labor'
  resource: string | null
  perCycle: number
  primary: Stat
  secondary: Stat
  skill: Skill
  needsMaterial: boolean
}

export interface StageDefinition {
  id: string
  name: string
  crew: number
  jobs: Array<{ jobId: string; amount: number }>
}

export interface BuildingDefinition {
  kind: BuildingKind
  name: string
  description: string
  requires: BuildingKind[]
  /** Empty for buildings of later ages. */
  stages: StageDefinition[]
  storage: StorageKind | null
  shelter: number
  shelterQuality: number
  /** Technology the village must discover before the building can be planned. */
  technology: string | null
}

export type Era = 'Wild' | 'Camp' | 'Village' | 'Town'

export const ERAS: Era[] = ['Wild', 'Camp', 'Village', 'Town']

/** A discovery the village researches together; the Tsar or a Governor puts a few to the vote and citizens choose the next one. */
export interface TechnologyDefinition {
  id: string
  name: string
  description: string
  era: Era
  /** Building where citizens research it. */
  researchedAt: BuildingKind
  /** Technologies to discover first. */
  requires: string[]
  /** Skills a citizen needs to take part in the research. */
  researcher: StatRequirement[]
  primary: Stat
  secondary: Stat
  /** Research work, in cycles of a researcher with every stat at 0. */
  cycles: number
  /** Different citizens who must research it. */
  crew: number
  bonuses: Array<{ taskId: string; multiplier: number }>
}

export interface TechnologyState {
  id: string
  discovered: boolean
  /** Not discovered and every technology it builds on is: the Tsar or a Governor can put it to the vote. */
  available: boolean
  done: number
  required: number
  hands: number
  crewNeeded: number
  knowledgeWork?:number
  knowledgeCost?:number
}

/** A vote on the next discovery. It closes once more than half of the present citizens voted; away ones are not counted. */
export interface TechnologyPoll {
  id: string
  options: Array<{ technologyId: string; votes: number }>
  ballots: Array<{ citizenId: string; technologyId: string }>
  /** Active citizens now, or when the poll closed. */
  electorate: number
  /** Votes that close the poll: more than half of `electorate`. */
  votesNeeded: number
  openedAt: string
  closedAt: string | null
  /** Null while open, or when the poll was cancelled. */
  winner: string | null
}

export interface TilePoint {
  x: number
  y: number
}

/** A rectangle of whole tiles. */
export interface TileArea {
  x: number
  y: number
  width: number
  height: number
}

export interface ProjectDefinition {
  id: string
  name: string
  description: string
  surveyCycles: number
  materials: ResourceAmount[]
  buildCycles: number
}

export type StorageKind = string

export interface StorageDefinition {
  resourceLimit?: number
  totalLimit?: number|null
  kind: StorageKind
  name: string
  /** Empty means every good. */
  accepts: string[]
  slots: number
  slotSize: number
}

export type StoryQuestKind =
  | 'Fire'
  | 'Shelter'
  | 'Stores'
  | 'FirstHouse'
  | 'Monument'
  | 'FishingSpot'
  | 'Palisade'
  | 'TsarBridge'
  | 'MonksGold'
  | 'MonasteryBell'
  | 'GreatGranary'

export type QuestKind = 'Hunger' | 'Cold' | 'Storage' | StoryQuestKind

const NEED_QUESTS: readonly QuestKind[] = ['Hunger', 'Cold', 'Storage']

/** Story quests are the village's chain of goals; the rest are needs that come and go. */
export const isStoryQuest = (kind: QuestKind): kind is StoryQuestKind => !NEED_QUESTS.includes(kind)

export interface QuestReward {
  /** Split between the helpers by their useful cycles. */
  renownPool: number
  morale: number
}

export interface Catalog {
  roles: Role[]
  skills: SkillDefinition[]
  tasks: Task[]
  projects: ProjectDefinition[]
  storages: StorageDefinition[]
  buildings: BuildingDefinition[]
  jobs: JobDefinition[]
  /** Land where a whole building footprint must fit. */
  buildArea: TileArea
  /** Ground nobody builds on: the campfire and the project sites. */
  reservedAreas: TileArea[]
  places: Record<string, TilePoint>
  questRewards: Partial<Record<QuestKind, QuestReward>>
  rules: Rules
  technologies: TechnologyDefinition[]
  crops: CropDefinition[]
  enemies: EnemyDefinition[]
  /** Best first: a citizen takes the first one available. */
  weapons: WeaponDefinition[]
  armors: ArmorDefinition[]
  heroClasses: HeroClassDefinition[]
  dungeonFoes: DungeonFoe[]
  dungeons: DungeonInfo[]
  /** Development and local deployments: helpers such as the day-night toggle are shown. */
  devTools: boolean
}

/** The rulers (the Tsar and the Governors) plan, citizens do the work. */
export type QuestAudience = 'Rulers' | 'Citizens'

export interface QuestObjective {
  id: string
  audience: QuestAudience
  done: boolean
  /** Progress numbers; null for planning objectives. */
  current: number | null
  required: number | null
}

/** A need of the tribe: opens when the village has a problem, closes when it is solved. */
export interface Quest {
  id: string
  kind: QuestKind
  /** The good a storage quest is about. */
  resource: string | null
  startedAt: string
  objectives: QuestObjective[]
}

export interface QuestCompletion {
  questId: string
  kind: QuestKind
  resource: string | null
  renownByCitizen: Record<string, number>
}

export interface SiteJob {
  reserved?: number
  jobId: string
  done: number
  required: number
  /** Work a labor job can take right now; labor cannot run ahead of the delivered material. */
  room: number
}

export interface SiteStage {
  id: string
  crewNeeded: number
  hands: number
  jobs: SiteJob[]
}

export interface Site {
  damage?:number
  ruinedAt?:string|null
  placedByAccountId?:string|null; ownerName?:string|null; placedAt?:string; boundaryConflict?:boolean; boundaryApproved?:boolean; hasWork?:boolean; operation?:string|null; publicDemolition?:boolean;
  ownerAccountId?: string|null
  targetLevel?: number
  kind: BuildingKind
  plotId: string
  number?: number
  displayNumber?: number
  stage: number
  /** 0 to 1. */
  progress: number
  stages: SiteStage[]
}

export interface Structure {
  ruinedAt?:string|null
  ownerAccountId?: string|null
  level?: number
  kind: BuildingKind
  plotId: string
  number?: number
  displayNumber?: number
  /** Harm done by raiders, 0 to `rules.maximumDamage`. */
  damage: number
}

export interface StorageSlot {
  kind: string
  /** 0 to 1. */
  fill: number
}

export interface Storage {
  ruinedAt?:string|null
  ownerAccountId?: string|null
  resourceLimit?: number
  totalLimit?: number | null
  kind: StorageKind
  plotId: string
  number?: number
  displayNumber?: number
  goods: ResourceAmount[]
  slots: StorageSlot[]
  /** Harm done by raiders, 0 to `rules.maximumDamage`. */
  damage: number
}

export interface ProjectMaterial {
  kind: string
  delivered: number
  required: number
}

export interface Project {
  stageName?:string|null
  bridgeStage?:number
  id: string
  definitionId: string
  name: string
  description: string
  phase: ProjectPhase
  progress: number
  surveyDone: number
  surveyRequired: number
  buildDone: number
  buildLimit: number
  buildRequired: number
  materials: ProjectMaterial[]
  activeCitizens: number
  supervisionCapacity: number
  buildersWorking: number
  /** Great works need an Engineer to survey and supervise; small works do not. */
  needsEngineer: boolean
}

const PROJECT_EFFECTS: readonly TaskEffect[] = ['Deliver', 'Build', 'Survey', 'Supervise']

export const isProjectTask = (task: Task) => PROJECT_EFFECTS.includes(task.effect)

/** Whether the running project has any use for a project task: its materials, survey and supervision. */
export function projectWants(project: Project | null, task: Task) {
  if (!isProjectTask(task)) return true
  if (!project) return false
  switch (task.effect) {
    case 'Deliver':
      return task.outputs.some((output) => project.materials.some((material) => material.kind === output.resource))
    case 'Survey':
      return project.surveyRequired > 0
    case 'Supervise':
      return project.needsEngineer
    default:
      return true
  }
}

/** Workplace of digging the gold vein on the far bank; mirrors the server's map. */
export const GOLD_VEIN = 'gold-vein'

/** Workplaces whose goods can be gathered at a spot picked on the map; mirrors the server's rules. */
export const SPOT_WORKPLACES: readonly string[] = ['forest', 'stones', 'berries', 'reeds']

export const takesSpot = (task: Task) =>
  task.effect === 'Resources' && task.hunt === null && task.inputs.length === 0 && SPOT_WORKPLACES.includes(task.workplace)

/** Whether the task digs the gold vein and the vein has nothing left. */
export const veinEmpty = (village: VillageState, task: Task) =>
  task.workplace === GOLD_VEIN && village.goldVeinRemaining <= 0

export interface ProjectCompletion {
  projectId: string
  projectName: string
  renownByCitizen: Record<string, number>
}

export interface Resource {
  kind: string
  amount: number
  /** Null when unlimited (knowledge). */
  capacity: number | null
}

/** A Synoptic's announcement; the weather it brought stays secret until its day or night comes. */
export interface Forecast {
  citizenId: string
  saysRain: boolean
  madeAt: string
}

/** Wind and thunderstorms are not drawn yet. */
export type WeatherKind = 'Clear' | 'Rain' | 'Wind' | 'Thunderstorm'

/** The sky of the current period: each day is a daytime followed by a night, and each draws its own weather. */
export interface Weather {
  kind: WeatherKind
  /** Rain or a thunderstorm: the rain effects apply. */
  raining: boolean
  night: boolean
  periodStartedAt: string
  /** When the current daytime or night ends and the next one's weather comes. */
  periodEndsAt: string
  /** The forecast made for the current period, if any. */
  current: Forecast | null
  /** The forecast for the next period; null until a Synoptic makes one. */
  next: Forecast | null
}

/** Rain's effects, mirroring the server's weather rules. */
export const RAIN = {
  fishingYield: 1.25,
  indoorYield: 1.15,
  fireMorale: 0.5,
  mudYield: 0.75,
  huntingYield: 0.8,
  outdoorEnergyCost: 1.5,
  underTreesRegeneration: 0.5,
  forestRegrowth: 2,
}

export interface CivicOrder { buildingKind?:BuildingKind|null; id:string; plotId:string; operation:string; requestedBy:string; partnerAccountId:string|null; partnerAccepted:boolean; status:string }
export interface Housing { plotId:string; beds:number; occupied:number; level:number; number?:number; ownerAccountId?:string|null; ownerName?:string|null; builtAt?:string|null }
export interface MonasteryView {open:boolean;inRange:boolean;knowsRemedy:boolean;readyAt:string|null;wood:number;food:number;remedyRoom:number;yard:TilePoint}
export interface VillageState {
  snapshotAt?: string
  bridgeDamage?:number
  bridgeRuinedAt?:string|null
  defeatedAt?:string|null
  relocations?:{plotId:string;residents:number;available:number}[]
  battles?: RaidBattle[]
  fortificationUpgrade?:FortificationQuote|null
  fortifications?: Fortification[]
  kingdomArea?: TileArea
  nextKingdomArea?:{x:number;y:number;width:number;height:number}|null
  castleLevel?: number
  civicOrders?: CivicOrder[]
  housing?: Housing[]
  bridgeOpened?: boolean
  id: string
  slug: string
  name: string
  description: string | null
  /** Joining needs the invite code; watching stays open to everyone. */
  isPrivate: boolean
  morale: number
  resources: Resource[]
  storages: Storage[]
  structures: Structure[]
  sites: Site[]
  project: Project | null
  completedProjects: string[]
  /** Open needs of the tribe, oldest first. */
  quests: Quest[]
  wildlife: Herd[]
  /** Standing trees on the camp land (`FOREST_AREA`); the wild beyond it is drawn from the village seed. */
  trees: Array<{ x: number; y: number }>
  /** The latest era the village discovered a technology of. */
  era: Era
  /** The technology the last vote chose; null until a vote chooses an undiscovered one. */
  currentResearch: string | null
  technologies: TechnologyState[]
  /** The open vote on the next discovery, or the last closed one. */
  poll: TechnologyPoll | null
  /** Gold still in the vein on the far bank; digging it stops at zero. */
  goldVeinRemaining: number
  /** Beds of every farm. */
  fields: FieldBed[]
  weather: Weather
  /** The village owner's figure on the map. */
  tsar: Tsar
  /**
   * Land the tribe has seen, shared by all its players, as base64: one bit per map tile, row by row from the
   * top-left, lowest bit first.
   */
  explored: string
  /** Attacks on the way (shown from `announcedAt`) and the last fought ones. */
  raids: Raid[]
  /** The ravine, mountain features and obstacles of the wild land; the map is shaped around them. */
  wilds: Wilds | null
  /** The dungeon run under way, or one that ended a moment ago. */
  dungeon: DungeonRun | null
}

/**
 * Generated once per village by the server. A pass is 2×2 tiles from its top-left tile; `horizontal` means its ridge
 * runs east to west. The cave mouth and the lookout top are single tiles.
 */
export interface Wilds {
  ravine: Array<{ x: number; top: number; height: number }>
  passes: Array<{ x: number; y: number; horizontal: boolean }>
  obstacles: Array<{ kind: 'Thicket' | 'Rockfall'; x: number; y: number; cleared: boolean }>
  caveX: number
  caveY: number
  lookoutX: number
  lookoutY: number
}

export interface Tsar {
  accountId?:string|null
  hospitalizedAt?:string|null
  rescueByCitizenId?:string|null
  rescueRequestedAt?:string|null
  injuredUntil?:string|null
  name: string
  /** Null until the owner creates their character. */
  appearance: Appearance | null
  /** Where the Tsar walked to; null while they stand at the campfire. */
  spot: TilePoint | null
}

export type Gender = 'Male' | 'Female'

/** Indexes into the lists in `game/appearance.ts`; the server checks them against the same counts. */
export interface Appearance {
  gender: Gender
  style: number
  skinTone: number
  hairColor: number
  outfitColor: number
}

export const CITIZEN_SUBTITLE_MAXIMUM = 24

export interface ExternalRecovery {
  cause?:string
  effectKind?:string|null
  effectUntil?:string|null
  episodeId: string | null
  admittedAt: string | null
  until: string | null
  checkedAt: string
  available: boolean
  hospitalPercent: number
  skillPercent: number
}
export interface HospitalTier { code: string; title: string; minHours: number; maxHours: number; price: number; blocked: string }
export interface HospitalStatus {
  available: boolean; level: number; balance: number | null; recovery: ExternalRecovery | null
  tiers: HospitalTier[]; patients: { id: string; name: string; recovery: ExternalRecovery; appearance?: Appearance|null }[]
}
export interface HospitalPayment { patientId: string; code: string; requestId: string; expectedPrice: number; episodeId: string }
export interface HospitalReceipt { state: string; error: string | null; until: number | null; price: number | null; cutSeconds: number | null }

export interface RescueJourney {
  patientId:string
  patientIsTsar:boolean
  hospitalPlotId:string
  pickupAt:string
  arrivesAt:string
  completedAt?:string|null
  delivered:boolean
  steps:{x:number;y:number;at:string}[]
}

export interface CitizenSummary {
  rescueJourney?:RescueJourney|null
  rescueRequestedAt?:string|null
  defenseSector?: string | null
  defenseReadyAt?: string | null
  hospitalizedAt?:string|null
  rescueByCitizenId?:string|null
  rescuePatientId?:string|null
  id: string
  name: string
  /** Written by the Tsar, shown under the name on the map. */
  subtitle: string | null
  /** Appointed by the Tsar: may plan buildings and call votes. */
  isGovernor: boolean
  /** Null for citizens who joined before character creation. */
  appearance: Appearance | null
  titleRoleId: string
  taskId: string | null
  workplace: string | null
  /** Plot the citizen works on: a construction site, or the building where they research. */
  siteId: string | null
  /** Spot on the map the citizen gathers at, picked by the player. */
  spot: TilePoint | null
  /** Where the player sent the citizen to stand while not working. */
  idleSpot: TilePoint | null
  state: WorkState
  cycleStartedAt: string | null
  cycleEndsAt: string | null
  /** Goods the citizen holds because storage had no room. */
  carrying: string[]
  /** Wounded until then; null when not wounded. */
  recovery?: ExternalRecovery | null
  injuredUntil: string | null
  /** Weapon taken from the armory for the current task. */
  weapon: string | null
}

export interface Village {
  state: VillageState
  isOwner: boolean
  /** Only sent to the owner of a private village. */
  inviteCode: string | null
  citizens: CitizenSummary[]
  /** Citizens with the village page closed for a while: not counted in votes, hidden from the map while idle. */
  awayCitizenIds: string[]
}

export interface VillageListItem {
  slug: string
  name: string
  isOwner: boolean
}

export interface PublicVillage {
  slug: string
  name: string
  description: string | null
  citizens: number
}

export interface CreateVillageRequest {
  name: string
  description: string | null
  isPrivate: boolean
}

export const VILLAGE_NAME_MINIMUM = 3
export const VILLAGE_NAME_MAXIMUM = 64
export const VILLAGE_DESCRIPTION_MAXIMUM = 280

/** Query parameter that carries the invite code of a private village. */
export const INVITE_PARAMETER = 'invite'

export function inviteLink(slug: string, inviteCode: string | null) {
  const link = `${window.location.origin}/v/${slug}`
  return inviteCode ? `${link}?${INVITE_PARAMETER}=${encodeURIComponent(inviteCode)}` : link
}

export interface StatValue {
  stat: Stat
  level: number
  experience: number
  experienceToNext: number
}

export interface SkillValue {
  skill: Skill
  level: number
  experience: number
  /** 0 at the maximum level. */
  experienceToNext: number
  /** Stats the next level needs; empty at the maximum level. */
  nextLevel: StatRequirement[]
  /** The experience for the next level is there, but the stats are not; it comes once they are. */
  waitsForStats: boolean
}

export interface Citizen {
  mealKind?: string | null
  mealUntil?: string | null
  mealReadyAt?: string | null
  vigorEnergy?: number
  vigorCooldownMinutes?: number
  accountId?: string
  cyclePrivateOutput?: boolean
  personalGoods?: {kind:string;amount:number;costBasis:number}[]
  actionEnergyCosts?: Record<string, number>
  homePlotId?:string|null
  spouseAccountId?:string|null
  spouseName?:string|null
  exhaustedUntil?:string|null
  exhaustedCycles?:number
  id: string
  name: string
  /** Written by the Tsar, shown under the name on the map. */
  subtitle: string | null
  /** Appointed by the Tsar: may plan buildings and call votes. */
  isGovernor: boolean
  appearance: Appearance | null
  titleRoleId: string
  status: 'Active' | 'Dormant'
  stats: StatValue[]
  statTotal: number
  /** Only the crafts the citizen has trained; any other is at level 0. */
  skills: SkillValue[]
  renown: number
  energy: number
  maximumEnergy: number
  streak: number
  taskId: string | null
  siteId: string | null
  siteStage: number | null
  /** Spot on the map the last task gathered at, picked by the player. */
  spot: TilePoint | null
  state: WorkState
  cycleStartedAt: string | null
  cycleEndsAt: string | null
  graceEndsAt: string | null
  cycleTired: boolean
  load: ResourceAmount[]
  /** Energy regeneration of where the citizen sleeps: 0.5 under the trees. */
  shelterQuality: number
  isHungry: boolean
  /** Finished tasks since the last meal; the citizen eats at `rules.cyclesPerMeal`. */
  cyclesSinceMeal: number
  /** Shelter quality, halved while hungry and again in the rain under the trees. */
  regenerationMultiplier: number
  /** Energy regained per hour at the current shelter, hunger and weather. */
  energyPerHour: number
  serverNow: string
  /** Technology the current cycle researches. */
  technologyId: string | null
  /** Chance the weather turns out opposite to this citizen's forecast; null if not a Synoptic. */
  forecastReliability: number | null
  /** When the next vigor drink may be drunk; null if never drunk. */
  drinkReadyAt: string | null
  /** Wounded until then; null when not wounded. */
  recovery?: ExternalRecovery | null
  injuredUntil: string | null
  weapon: string | null
  /** Edge of the weapon in hand. 0 is an ordinary weapon. */
  weaponSharpen?: number
  armor: string | null
  arrows: number
  /** Edge of the arrows in the quiver. 0 is an ordinary arrow. */
  arrowSharpen?: number
  /** Fight power with the weapon in hand. */
  power: number
}

export interface CycleReport {
  citizenId: string
  citizenName: string
  taskId: string
  produced: ResourceAmount[]
  /** Part of `produced` that did not fit in storage and stays in the citizen's hands. */
  carried: ResourceAmount[]
  /** Part of `produced` that fit neither in storage nor in the citizen's hands. */
  lost: ResourceAmount[]
  moraleGained: number
  projectContribution: number
  rareFind: boolean
  primary: Stat
  primaryExperience: number
  secondary: Stat
  secondaryExperience: number
  skill: Skill
  skillExperience: number
  skillLevelsGained: number
  /** Stat levels; skill levels are in `skillLevelsGained`. */
  levelsGained: number
  renownGained: number
  tired: boolean
  overflowed: boolean
  siteId: string | null
  buildingCompleted: BuildingKind | null
  hungry: boolean
  /** The cycle started while hungry, so experience was halved. A meal missed only at the end leaves this false. */
  workedHungry?: boolean
  /** Technology the cycle researched. */
  technologyId: string | null
  /** This cycle finished the research of `technologyId`. */
  technologyDiscovered: boolean
  /** A beast attacked on the way; null when nothing did. */
  fight: FightReport | null
  /** Damage mended by a repair task. */
  targetPlotId?: string | null
  repaired: number
}

/** Events of one village since the last `villageEvents` message, in order; only the latest summary per citizen. */
export interface VillageEvents {
  battles?: RaidBattle[]
  fortificationUpgrade?:FortificationQuote|null
  fortifications?: Fortification[] | null
  cycles: CycleReport[]
  projects: ProjectCompletion[]
  quests: QuestCompletion[]
  citizens: CitizenSummary[]
  raids: RaidReport[]
  /** Dungeon runs that ended. */
  dungeons: DungeonRun[]
  /** Ids of citizens who left the village for good. */
  left: string[]
  /** Ids of citizens who went away; they still live in the village. */
  away: string[]
  /** Ids of citizens who came back. */
  back: string[]
}

export const CHAT_MESSAGE_MAXIMUM = 120

/** A line in the village chat; only the Tsar and the citizens read and write it. */
export interface ChatMessage {
  mentionCitizenIds?: string[]|null
  id: string
  authorName: string
  /** Null when the Tsar wrote it. */
  citizenId: string | null
  isTsar: boolean
  isChaplain?: boolean
  text: string
  sentAt: string
}

/** One per account, played in every village; each village keeps its own progress. */
export interface Character {
  name: string
  appearance: Appearance
}

export interface Me {
  id: string
  displayName: string
  providers: string[]
  /** Null until the player creates it after signing up. */
  character: Character | null
}

export interface LoginProviders {
  twitch: boolean
  google: boolean
  telegramBotUsername: string | null
  /** Email and password, without any outside service. */
  password: boolean
  primaryVillage?: string | null
  telegramBrowserLogin: boolean
  developmentLogin: boolean
  /** Present only in Development: ready Tsar, Governor and villager logins. */
  developmentRoles?: { role: string; name: string }[] | null
}

export const PASSWORD_MINIMUM = 8
export const PASSWORD_MAXIMUM = 128
export const EMAIL_MAXIMUM = 254
export const DISPLAY_NAME_MAXIMUM = 32

/** `code` and `parameters` come from the server's problem details so the message can be translated. */
export class ApiError extends Error {
  readonly status: number
  readonly code: string | null
  readonly parameters: Record<string, string | number>

  constructor(status: number, message: string, code: string | null = null, parameters: Record<string, string | number> = {}) {
    super(message)
    this.status = status
    this.code = code
    this.parameters = parameters
  }
}

async function request<T>(method: string, url: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const response = await fetch(appPath(url), {
    method,
    credentials: 'same-origin',
    signal,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (!response.ok) {
    let message = response.statusText
    let code: string | null = null
    let parameters: Record<string, string | number> = {}
    try {
      const problem = await response.json()
      message = problem.detail ?? problem.title ?? message
      code = problem.code ?? null
      parameters = problem.arguments ?? {}
    } catch {
      // Body was not JSON.
    }
    throw new ApiError(response.status, message, code, parameters)
  }

  const text = response.status === 204 ? '' : await response.text()
  return text ? JSON.parse(text) as T : undefined as T
}

async function orNull<T>(promise: Promise<T>): Promise<T | null> {
  try {
    return await promise
  } catch (error) {
    if (error instanceof ApiError && (error.status === 401 || error.status === 404)) {
      return null
    }
    throw error
  }
}

let prefetchedVillage: { slug: string; promise: Promise<Village> } | null = null

/** Starts loading the village of a `/v/:slug` address while the map page's code is still downloading. */
export function prefetchVillage(path: string) {
  const match = /^\/v\/([^/]+)/.exec(path.slice(appBase.length))
  if (!match) return
  const slug = decodeURIComponent(match[1])
  const promise = api.village(slug)
  // The page reports a failure when it takes the promise.
  promise.catch(() => {})
  prefetchedVillage = { slug, promise }
}

/**
 * The prefetched village when it is for `slug`, at most once; otherwise a fresh request.
 * A failed prefetch is retried, because the visitor may have signed in since.
 */
export function loadVillage(slug: string) {
  const prefetched = prefetchedVillage
  prefetchedVillage = null
  return prefetched?.slug === slug ? prefetched.promise.catch(() => api.village(slug)) : api.village(slug)
}

export interface AlmightyProposal {
  id: string; option: string; title?: string; effect?: string; reason: string; intent: string; status: string;
  createdAt: string; expiresAt: string; noticeState: string; result: string
}
export interface AlmightyView {
  enabled: boolean;
  state: { mode: string; intent: string; lastReviewAt: string | null; nextReviewAt: string; restUntil: string | null; lastError: string; callsToday: number; lastSnapshot: string };
  proposals: AlmightyProposal[];
  observations: { id: string; at: string; option: string; reason: string; snapshot: string }[]
}

export function createOperationId() {
  const bytes=crypto.getRandomValues(new Uint8Array(16)); bytes[6]=(bytes[6]!&15)|64; bytes[8]=(bytes[8]!&63)|128
  const id=Array.from(bytes,b=>b.toString(16).padStart(2,'0')).join('')
  return `${id.slice(0,8)}-${id.slice(8,12)}-${id.slice(12,16)}-${id.slice(16,20)}-${id.slice(20)}`
}
export interface EconomyRequest {
  requestId: string; action: string; resource?: string; amount?: number; price?: number; target?: string; x?: number; y?: number; building?: string
}
export interface EconomyCurrencyIdentity { id: string; ledgerAsset: string }

export interface EconomyView {
  /** Optional while old servers still expose only coins/testCandles. Balances and asset keys do not change. */
  currencies?: { coins: EconomyCurrencyIdentity; testCandles: EconomyCurrencyIdentity }
  siteRecoveries?:{id:string;plotId:string;personal:boolean;goods:{resource:string;amount:number}[]}[]
  energyDrinkReadyAt: string | null
  stockPrices: Record<string,number>
  privateProperty: {plotId:string;kind:string;level:number;site:boolean;hasWork:boolean}[]
  coins: number; testCandles: number; earnedToday: number; profitToday: number; taxPreview: number; taxEnabled: boolean
  exchangeRate: number; dailyExchangeLimit: number; exchangedToday: number; kingdomCandleRoom: number; dayResetsAt: string; treasury: number
  inventory: {kind: string; amount: number; costBasis: number}[]
  lots: {id: string; sellerAccountId: string; seller: string; resource: string; amount: number; price: number; status: string}[]
  journal: {id: string; at: string; operation: string; asset: string; location: string; delta: number; actor: string|null}[]
  nextJournalBefore: string|null; startedAt: string; workPay: number; constructionPay: number
  residents: {accountId: string; name: string}[]
  privateStorages: {plotId: string; kind: string; goods: {resource:string; amount:number; sharpen?:number}[]}[]
}
export const api = {
  submitReport: (body: {id:string;kind:string;text:string;slug:string|null;capturedAt:string;screen:unknown;screenshot:string|null}) =>
    request<{id:string;state:string;screenshotIncluded?:boolean}>('POST', '/api/reports', body, AbortSignal.timeout(20000)),
  economy: (slug: string, before?: string, asset?: string) => request<EconomyView>('GET', `/api/villages/${encodeURIComponent(slug)}/economy?${new URLSearchParams({...before ? {before} : {}, ...asset ? {asset} : {}})}`),
  economyAction: (slug: string, body: EconomyRequest) => request<EconomyView>('POST', `/api/villages/${encodeURIComponent(slug)}/economy`, body).then(result=>{
    window.dispatchEvent(new CustomEvent('desertovia-wallet-changed',{detail:{slug,coins:result.coins,testCandles:result.testCandles}}));return result
  }),
  combatDiagnostics:(slug:string)=>request<unknown>('GET',`/api/villages/${encodeURIComponent(slug)}/combat-diagnostics`),
  monastery:(slug:string)=>request<MonasteryView>('GET',`/api/villages/${encodeURIComponent(slug)}/monastery`),
  monasteryTrade:(slug:string,requestId:string)=>request<MonasteryView>('POST',`/api/villages/${encodeURIComponent(slug)}/monastery`,{requestId}),
  battleOrder: (slug:string,battleId:string,targetUnitId:string|null,mode:'hold'|'intercept'|'retreat'|'heal'='intercept',destination?:TilePoint,requestId?:string) =>
    request<void>('PUT',`/api/villages/${encodeURIComponent(slug)}/battles/${battleId}/order`,{targetUnitId,mode,destination,requestId}),
  gateOrder: (slug:string,open:boolean) => request<void>('PUT',`/api/villages/${encodeURIComponent(slug)}/gate`,{open}),
  defenseSector: (slug: string, sector: string) => request<void>('PUT', `/api/villages/${encodeURIComponent(slug)}/me/defense`, { mode: sector }),
  almighty: (slug: string) => request<AlmightyView>('GET', `/api/villages/${encodeURIComponent(slug)}/director`),
  almightyMode: (slug: string, mode: string) => request<void>('PUT', `/api/villages/${encodeURIComponent(slug)}/director/mode`, { mode }),
  almightyVote: (slug: string, id: string, accept: boolean) => request<{result:string}>('POST', `/api/villages/${encodeURIComponent(slug)}/director/${id}/vote`, { accept }),
  rescue: (slug:string, id:string)=>request<Citizen>('POST',`/api/villages/${encodeURIComponent(slug)}/rescue/${id}`),
  requestRescue: (slug:string)=>request<boolean>('POST',`/api/villages/${encodeURIComponent(slug)}/rescue/request`),
  civic: (slug:string, plotId:string, operation:string, partnerAccountId?:string) => request<VillageState>('POST', `/api/villages/${encodeURIComponent(slug)}/civic`, {plotId,operation,partnerAccountId}),
  civicDecision: (slug:string, id:string, approve:boolean) => request<VillageState>('POST', `/api/villages/${encodeURIComponent(slug)}/civic/${id}`, {approve}),
  sramoslav: (slug: string, text: string) => request<{text: string; ai: boolean}>('POST', `/api/villages/${slug}/sramoslav`, { text }),
  telegramMiniApp: (initData: string) => request<void>('POST', '/auth/telegram/miniapp', { initData }),
  telegramBrowserStart: () => request<{url: string; pin: string}>('POST', '/auth/telegram/browser/start'),
  telegramBrowserPoll: () => request<{pending: boolean}>('POST', '/auth/telegram/browser/poll'),
  providers: () => request<LoginProviders>('GET', '/auth/providers'),
  me: () => orNull(request<Me>('GET', '/api/me')),
  developmentLogin: (name: string) => request<void>('POST', '/auth/dev-login', { name }),
  passwordLogin: (email: string, password: string) => request<void>('POST', '/auth/password/login', { email, password }),
  passwordRegister: (email: string, password: string, name: string) =>
    request<void>('POST', '/auth/password/register', { email, password, name }),
  logout: () => request<void>('POST', '/auth/logout'),
  createCharacter: (name: string, appearance: Appearance) => request<Me>('POST', '/api/me/character', { name, appearance }),

  catalog: () => request<Catalog>('GET', '/api/catalog'),
  villages: () => request<VillageListItem[]>('GET', '/api/villages'),
  publicVillages: () => request<PublicVillage[]>('GET', '/api/villages/public'),
  createVillage: (village: CreateVillageRequest) => request<VillageState>('POST', '/api/villages', village),
  village: (slug: string) => request<Village>('GET', `/api/villages/${encodeURIComponent(slug)}`),
  newInviteCode: (slug: string) =>
    request<{ inviteCode: string }>('POST', `/api/villages/${encodeURIComponent(slug)}/invite`),

  myCitizen: (slug: string) => orNull(request<Citizen>('GET', `/api/villages/${encodeURIComponent(slug)}/me`)),
  joinVillage: (slug: string, inviteCode: string | null) =>
    request<Citizen>('POST', `/api/villages/${encodeURIComponent(slug)}/citizens`, { inviteCode }),
  /** The citizen leaves for good: carried goods go to storage, and joining again starts a new citizen. */
  leaveVillage: (slug: string) => request<void>('DELETE', `/api/villages/${encodeURIComponent(slug)}/me`),
  /** The Tsar deletes the village with everything in it. */
  deleteVillage: (slug: string) => request<void>('DELETE', `/api/villages/${encodeURIComponent(slug)}`),
  startTask: (slug: string, taskId: string, spot: TilePoint | null = null, workplacePlotId?: string) =>
    request<Citizen>('POST', `/api/villages/${encodeURIComponent(slug)}/me/task`, { taskId, spot, workplacePlotId }),
  cancelTask: (slug: string) => request<Citizen>('DELETE', `/api/villages/${encodeURIComponent(slug)}/me/task`),
  startJob: (slug: string, plotId: string, stage: number, jobId: string) =>
    request<Citizen>('POST', `/api/villages/${encodeURIComponent(slug)}/me/job`, { plotId, stage, jobId }),
  dropLoad: (slug: string) => request<Citizen>('POST', `/api/villages/${encodeURIComponent(slug)}/me/drop`),
  investKnowledge: (slug:string,technologyId:string)=>request<VillageState>('POST',`/api/villages/${encodeURIComponent(slug)}/research/knowledge`,{technologyId}),
  eatMeal: (slug:string,resource:string)=>request<Citizen>('POST',`/api/villages/${encodeURIComponent(slug)}/me/meal`,{resource}),
  eat: (slug: string) => request<Citizen>('POST', `/api/villages/${encodeURIComponent(slug)}/me/eat`),
  drink: (slug: string) => request<Citizen>('POST', `/api/villages/${encodeURIComponent(slug)}/me/drink`),
  hospital: (slug: string) => request<HospitalStatus>('GET', `/api/villages/${encodeURIComponent(slug)}/hospital`),
  hospitalHeal: (slug: string, payment: HospitalPayment) => request<HospitalReceipt>('POST', `/api/villages/${encodeURIComponent(slug)}/hospital/heal`, payment),
  hospitalUpgrade: (slug: string, plot: string) => request<VillageState>('POST', `/api/villages/${encodeURIComponent(slug)}/hospital/${encodeURIComponent(plot)}/upgrade`),
  heal: (slug: string) => request<Citizen>('POST', `/api/villages/${encodeURIComponent(slug)}/me/heal`),
  upgradeFortifications: (slug:string) => request<VillageState>('POST', `/api/villages/${encodeURIComponent(slug)}/fortifications/upgrade`),
  raidEditor: (slug:string) => request<RaidEditorView>('GET', `/api/villages/${encodeURIComponent(slug)}/raid-editor`),
  previewRaid: (slug:string, settings:RaidSettings) => request<RaidPreview>('POST', `/api/villages/${encodeURIComponent(slug)}/raid-editor/preview`, settings),
  launchRaid: (slug:string, requestId:string, settings:RaidSettings) => request<VillageState>('POST', `/api/villages/${encodeURIComponent(slug)}/raid-editor/launch`, {requestId,settings}),
  saveRaidPreset: (slug:string, id:string|null, name:string, settings:RaidSettings) => request<RaidEditorView>('PUT', `/api/villages/${encodeURIComponent(slug)}/raid-editor/presets`, {id,name,settings}),
  callUndead: (slug: string, enemy: EnemyKind, size: RaidSize) =>
    request<VillageState>('POST', `/api/villages/${encodeURIComponent(slug)}/undead`, { enemy, size }),
  cancelUndead: (slug: string) => request<VillageState>('DELETE', `/api/villages/${encodeURIComponent(slug)}/undead`),
  takeFromStorage: (slug: string, plotId: string, resource: string, amount: number, sharpen?: number) =>
    request<Citizen>('POST', `/api/villages/${encodeURIComponent(slug)}/me/take`, { plotId, resource, amount, sharpen }),
  goTo: (slug: string, spot: TilePoint) => request<Citizen>('POST', `/api/villages/${encodeURIComponent(slug)}/me/goto`, { spot }),
  research: (slug: string) => request<Citizen>('POST', `/api/villages/${encodeURIComponent(slug)}/me/research`),
  forecast: (slug: string, rain: boolean) =>
    request<VillageState>('POST', `/api/villages/${encodeURIComponent(slug)}/me/forecast`, { rain }),
  vote: (slug: string, technologyId: string) =>
    request<VillageState>('POST', `/api/villages/${encodeURIComponent(slug)}/me/vote`, { technologyId }),
  openPoll: (slug: string, technologyIds: string[]) =>
    request<VillageState>('POST', `/api/villages/${encodeURIComponent(slug)}/poll`, { technologyIds }),
  cancelPoll: (slug: string) => request<VillageState>('DELETE', `/api/villages/${encodeURIComponent(slug)}/poll`),
  tsarGoTo: (slug: string, spot: TilePoint) =>
    request<VillageState>('POST', `/api/villages/${encodeURIComponent(slug)}/tsar/goto`, { spot }),
  placeBlueprint: (slug: string, kind: BuildingKind, x: number, y: number) =>
    request<VillageState>('POST', `/api/villages/${encodeURIComponent(slug)}/blueprints`, { kind, x, y }),
  cancelBlueprint: (slug: string, plotId: string) =>
    request<VillageState>('DELETE', `/api/villages/${encodeURIComponent(slug)}/blueprints/${encodeURIComponent(plotId)}`),
  setSubtitle: (slug: string, citizenId: string, subtitle: string | null) =>
    request<CitizenSummary>(
      'PUT',
      `/api/villages/${encodeURIComponent(slug)}/citizens/${encodeURIComponent(citizenId)}/subtitle`,
      { subtitle },
    ),
  setGovernor: (slug: string, citizenId: string, isGovernor: boolean) =>
    request<CitizenSummary>(
      'PUT',
      `/api/villages/${encodeURIComponent(slug)}/citizens/${encodeURIComponent(citizenId)}/governor`,
      { isGovernor },
    ),
  openDungeon: (slug: string, dungeonId: string = DEAD_LANDS) =>
    request<VillageState>('POST', `/api/villages/${encodeURIComponent(slug)}/dungeon`, { dungeonId }),
  cancelDungeon: (slug: string) => request<VillageState>('DELETE', `/api/villages/${encodeURIComponent(slug)}/dungeon`),
  heroClasses: (slug: string) => request<HeroClassOffer[]>('GET', `/api/villages/${encodeURIComponent(slug)}/me/dungeon`),
  joinDungeon: (slug: string, heroClass: HeroClass) =>
    request<Citizen>('POST', `/api/villages/${encodeURIComponent(slug)}/me/dungeon`, { class: heroClass }),
  leaveDungeon: (slug: string) => request<Citizen>('DELETE', `/api/villages/${encodeURIComponent(slug)}/me/dungeon`),
  chat: (slug: string) => request<ChatMessage[]>('GET', `/api/villages/${encodeURIComponent(slug)}/chat`),
  sendChat: (slug: string, text: string, mentions?: string[]) =>
    request<ChatMessage>('POST', `/api/villages/${encodeURIComponent(slug)}/chat`, { text, mentions }),
}

export const LEND_A_HAND = 'lend-a-hand'
export const CARRY_TO_STORAGE = 'carry-to-storage'

/** The guard task anyone can take when an attack is coming. */
export const KEEP_WATCH = 'keep-watch'
/** Task id of a research cycle; the technology is reported separately. */
export const RESEARCH_TASK = 'research-technology'

export function discoveredTechnologies(village: VillageState) {
  return new Set(village.technologies.filter((technology) => technology.discovered).map((technology) => technology.id))
}

/** Buildable buildings that store a good, with specialised ones first. */
export function storageBuildingsFor(catalog: Catalog, resource: string) {
  return catalog.buildings.filter((building) => {
    const storage = catalog.storages.find((s) => s.kind === building.storage)
    return building.stages.length > 0 && storage !== undefined && storage.accepts.includes(resource)
  })
}

export function builtKinds(village: VillageState, catalog: Catalog) {
  const kinds = new Set<BuildingKind>(village.structures.filter(s=>!s.ownerAccountId).map((s) => s.kind))
  for (const storage of village.storages.filter(s=>!s.ownerAccountId)) {
    const building = catalog.buildings.find((b) => b.storage === storage.kind)
    if (building) kinds.add(building.kind)
  }
  return kinds
}

/** Tasks whose building, project and technology the village already has, so the players may hear of them. */
export function unlockedTasks(village: VillageState, catalog: Catalog) {
  const built = new Set<string>([...builtKinds(village, catalog), ...village.completedProjects])
  const discovered = discoveredTechnologies(village)
  const known = (technology: string | null | undefined) => !technology || discovered.has(technology)
  return new Set(
    catalog.tasks
      .filter(
        (task) =>
          (!task.requiredBuilding || built.has(task.requiredBuilding)) &&
          (!task.requiredProject || built.has(task.requiredProject)) &&
          known(task.requiredTechnology) &&
          known(catalog.roles.find((role) => role.id === task.roleId)?.technology),
      )
      .map((task) => task.id),
  )
}

/** Built, stored and planned buildings of a kind; mirrors the server's count for the repeat cost. */
export function buildingCount(village: VillageState, catalog: Catalog, kind: BuildingKind) {
  const storageKind = catalog.buildings.find((b) => b.kind === kind)?.storage
  return (
    village.structures.filter((s) => s.kind === kind && !s.ownerAccountId).length +
    (storageKind ? village.storages.filter((s) => s.kind === storageKind && !s.ownerAccountId).length : 0) +
    village.sites.filter((s) => s.kind === kind && !s.ownerAccountId).length
  )
}

/** Goods every stage of a building needs carried in, rounded up per job as the server does. */
export function buildingMaterials(catalog: Catalog, definition: BuildingDefinition, scale: number) {
  const totals = new Map<string, number>()
  for (const stage of definition.stages) {
    for (const job of stage.jobs) {
      const resource = catalog.jobs.find((j) => j.id === job.jobId && j.kind === 'Haul')?.resource
      if (resource) totals.set(resource, (totals.get(resource) ?? 0) + Math.ceil(job.amount * scale))
    }
  }
  return [...totals].map(([resource, amount]) => ({ resource, amount }))
}

/** Top-left tile of a plot, whose id is that tile written as "x,y"; null for an id that is not a plot. Mirrors the server's map. */
export function plotPosition(plotId: string): TilePoint | null {
  const match = /^(-?\d+),(-?\d+)$/.exec(plotId)
  return match ? { x: Number(match[1]), y: Number(match[2]) } : null
}

export function siteHasWork(site: Site) {
  return site.hasWork === true || site.stages.some((stage) => stage.hands > 0 || stage.jobs.some((job) => job.done > 0))
}

/**
 * A good the task produces that fits neither in storage nor in the citizen's hands; the task is blocked only
 * when that holds for every good it produces. Mirrors the server's hands check.
 */
export function blockedByLoad(task: Task, citizen: Citizen, resources: Resource[], takeLimit: number) {
  if (task.effect === 'Expedition' || task.effect === 'Roam') return citizen.load.length > 0 ? citizen.load[0].resource : null
  if (task.effect !== 'Resources') return null
  const noRoom = (resource: string) => {
    const held = citizen.load.find((good) => good.resource === resource)?.amount ?? 0
    const stock = resources.find((good) => good.kind === resource)
    const storageFull = stock !== undefined && stock.capacity !== null && stock.amount >= stock.capacity
    return held >= takeLimit && storageFull
  }
  const blocked = task.outputs.map((output) => output.resource)
  return blocked.length > 0 && blocked.every(noRoom) ? blocked[0] : null
}
