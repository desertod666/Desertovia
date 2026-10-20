import { usePopupGesture } from './usePopupGesture'
import {canUpgradeBuilding} from '../buildingLabels'
import { buildingNumber } from './buildingLabel'
import { SiteActions } from './SiteActions'
import { useState } from 'react'
import {
  builtKinds,
  CARRY_TO_STORAGE,
  discoveredTechnologies,
  isProjectTask,
  projectWants,
  takesSpot,
  type Catalog,
  type Citizen,
  type CitizenSummary,
  type FarmWorkTarget,
  type Task,
  type TilePoint,
  type VillageState,
} from '../api'
import { canReachBank, inForestArea, type NatureKind, type WorldTarget } from '../game/worldMap'
import { useI18n, type Translator } from '../i18n'
import type { UiKey } from '../i18n/en'
import { FieldsCard } from './FieldsCard'
import { FarmPlotBoard } from './FarmPlotBoard'
import { FarmHistory } from './FarmHistory'
import { SiteCard, type JobChoice } from './SitesPanel'
import { StorageCard } from './StorageCard'
import { TaskList } from './TaskList'

/** Workplaces of the tasks a natural place offers. */
const NATURE_WORKPLACES: Record<NatureKind, string[]> = {
  tree: ['forest'],
  bush: ['berries'],
  stone: ['stones'],
  reeds: ['reeds', 'ford', 'pier'],
  water: ['pier', 'ford', 'reeds'],
  vein: ['gold-vein'],
  thicket: ['thicket'],
  rockfall: ['rockfall'],
  cave: ['cave'],
}

/** A picked spot is only kept where the citizen really gathers what the place offers. */
const SPOT_KINDS: readonly NatureKind[] = ['tree', 'bush', 'stone', 'reeds']

/** Places in the wild the server knows: the picked tile tells it which thicket or rockfall to clear. */
const WILD_KINDS: readonly NatureKind[] = ['thicket', 'rockfall', 'cave']

const NATURE_TITLES: Record<NatureKind, UiKey> = {
  tree: 'target.tree',
  bush: 'target.bush',
  stone: 'target.stone',
  reeds: 'target.reeds',
  water: 'target.water',
  vein: 'target.vein',
  thicket: 'target.thicket',
  rockfall: 'target.rockfall',
  cave: 'target.cave',
}

function damageOf(village: VillageState, plotId: string) {
  return (
    village.structures.find((structure) => structure.plotId === plotId)?.damage ??
    village.storages.find((storage) => storage.plotId === plotId)?.damage ??
    village.sites.find((site) => site.plotId === plotId)?.damage ??
    0
  )
}

function tasksFor(target: WorldTarget, catalog: Catalog, village: VillageState): Task[] {
  switch (target.kind) {
    case 'site':
      return damageOf(village,target.plotId)>0 ? catalog.tasks.filter(task=>task.effect==='Repair') : []
    case 'animal':
      return catalog.tasks.filter((task) => task.hunt?.prey === target.prey)
    case 'campfire':
      return catalog.tasks.filter((task) => task.workplace === 'camp' && task.effect === 'Morale')
    case 'project':
      return catalog.tasks.filter((task) => isProjectTask(task) && projectWants(village.project, task))
    case 'building': {
      const damaged = damageOf(village, target.plotId) > 0
      return catalog.tasks.filter(
        (task) =>
          task.requiredBuilding === target.building ||
          (target.storage && !village.storages.find(s=>s.plotId===target.plotId)?.ownerAccountId && task.id === CARRY_TO_STORAGE) ||
          (damaged && task.effect === 'Repair'),
      )
    }
    default: {
      const workplaces = NATURE_WORKPLACES[target.kind]
      return catalog.tasks.filter((task) => workplaces.includes(task.workplace) && projectWants(village.project, task))
    }
  }
}

function titleOf(target: WorldTarget, catalog: Catalog, citizen: Citizen, i18n: Translator, village: VillageState) {
  switch (target.kind) {
    case 'site':
    case 'building':
      return `${i18n.building(target.building).name} ${buildingNumber(village, target.plotId)}`
    case 'project':
      return i18n.project(target.projectId).name
    case 'campfire':
      return i18n.t('target.campfire')
    case 'animal': {
      // Prey the citizen is not skilled enough for stays a mystery, as in the task list.
      const needed = catalog.tasks.find((task) => task.hunt?.prey === target.prey)?.skillLevel ?? 0
      const hunting = citizen.skills.find((skill) => skill.skill === 'Hunting')?.level ?? 0
      return hunting >= needed ? i18n.animal(target.prey) : i18n.t('task.preyUnknown')
    }
    default:
      return i18n.t(NATURE_TITLES[target.kind])
  }
}

/** Spot sent with a gathering task, so the citizen works right at the clicked tree, bush or stone. */
function spotFor(target: WorldTarget, task: Task): TilePoint | null {
  if (!('spot' in target)) return null
  if (WILD_KINDS.includes(target.kind) && task.workplace === target.kind) return target.spot
  if (!SPOT_KINDS.includes(target.kind) || !takesSpot(task)) return null
  return target.spot
}

/** Opens below the click in the top half of the screen and above it in the bottom half; phones get a bottom sheet. */
export function popupStyle(screen: { x: number; y: number }, phone: boolean, width = 340,preferredHeight?:number) {
  if (phone) return undefined
  const below = screen.y < window.innerHeight / 2
  const gap = 18
  const margin = 8
  if(preferredHeight!==undefined){
    const height=Math.min(preferredHeight,window.innerHeight-margin*2)
    return {left:Math.max(margin,Math.min(screen.x-width/2,window.innerWidth-width-margin)),top:Math.max(margin,Math.min(screen.y-80,window.innerHeight-height-margin)),maxHeight:height}
  }
  return {
    left: Math.max(margin, Math.min(screen.x - width / 2, window.innerWidth - width - margin)),
    ...(below
      ? { top: screen.y + gap, maxHeight: window.innerHeight - screen.y - gap - margin }
      : { bottom: window.innerHeight - screen.y + gap, maxHeight: screen.y - gap - margin }),
  }
}

/** What the player can send their citizen to do at the place they clicked on the map. */
export function TargetPopup({
  target,
  screen,
  phone,
  village,
  catalog,
  citizen,
  now,
  pending,
  error,
  onStart,
  onStartJob,
  onTake,
  onCivic,
  ruler,
  onClose,
  slug,
  workers=[],
  initialFarmTaskId,
}: {
  target: WorldTarget
  /** Where the map was clicked, in pixels from the top-left corner of the game. */
  screen: { x: number; y: number }
  phone: boolean
  village: VillageState
  catalog: Catalog
  citizen: Citizen
  now: number
  pending: boolean
  error: string | null
  onStart: (task: Task, spot: TilePoint | null, workplacePlotId?: string, farmTarget?:FarmWorkTarget) => void
  onStartJob: (choice: JobChoice) => void
  ruler: boolean
  onCivic: (plotId:string, operation:string)=>Promise<void>
  onTake: (plotId: string, resource: string, amount: number, sharpen?: number, durability?: number) => void
  onClose: () => void
  slug?:string
  workers?:CitizenSummary[]
  initialFarmTaskId?:string
}) {
  const [confirm,setConfirm]=useState(false),[operationError,setOperationError]=useState('')
  const operate=async(operation:string)=>{if(target.kind!=='building')return;try{await onCivic(target.plotId,operation);setConfirm(false)}catch(e){setOperationError(e instanceof Error?e.message:'Не удалось выполнить действие')}}
  const gesture = usePopupGesture()
  const i18n = useI18n()
  const { t } = i18n
  const working = citizen.cycleEndsAt ? now < Date.parse(citizen.cycleEndsAt) : false
  const blockedBank='spot' in target && !canReachBank(village,target.spot)
  const disabled = blockedBank || pending || working || citizen.state === 'Paused'||citizen.restState==='Resting'||citizen.restState==='RestRequested'
  const site = target.kind === 'site' ? village.sites.find((s) => s.plotId === target.plotId) : undefined
  const beds = target.kind === 'building' ? village.fields.filter((bed) => bed.plotId === target.plotId) : []
  const addressedFarm=beds.length>0&&beds.every(b=>!!b.generationId)
  const farmDenied=addressedFarm&&village.farmerAdmissionEnabled&&!citizen.isFarmer&&!village.structures.find(s=>s.plotId===beds[0].plotId)?.ownerAccountId
  const tasks = tasksFor(target, catalog, village).filter(task=>!addressedFarm||!['Sow','Water','Harvest'].includes(task.effect)&&task.id!=='tend-fields')
  const storage =
    target.kind === 'building' && target.storage ? village.storages.find((s) => s.plotId === target.plotId) : undefined
  const built = new Set<string>([...builtKinds(village, catalog), ...village.structures.filter(s=>s.ownerAccountId===citizen.accountId).map(s=>s.kind), ...village.completedProjects])
  const wildWood =
    target.kind === 'tree' && !inForestArea(Math.floor(target.spot.x), Math.floor(target.spot.y))
  const home=target.kind==='building'?village.housing?.find(h=>h.plotId===target.plotId):undefined
  const damage = target.kind === 'building' ? damageOf(village, target.plotId) : 0

  return (
    <div {...gesture} className={`target-popup ${phone ? 'sheet' : ''} ${addressedFarm ? 'farm-popup' : ''}`} style={popupStyle(screen, phone, addressedFarm ? 520 : 340,addressedFarm?600:undefined)} role="dialog" aria-label={titleOf(target, catalog, citizen, i18n, village)}>
      <header className="target-popup-header">
        <strong>{titleOf(target, catalog, citizen, i18n, village)}</strong>
        <button className="close" onClick={onClose} title={`${t('close')} (Esc)`}>
          ✕
        </button>
      </header>
      <div className="target-popup-body">
        {home?.ownerAccountId&&<section className="small"><p>{i18n.language==='ru'?'Владелец':'Owner'}: {home.ownerName??'—'}{home.builtAt&&` · ${new Date(home.builtAt).toLocaleDateString(i18n.language)}`}</p><p>{i18n.language==='ru'?'Занято мест':'Occupied beds'}: {home.occupied}/{home.beds}</p>{home.ownerAccountId===citizen.accountId&&citizen.homePlotId!==home.plotId&&<button disabled={pending||home.occupied>=home.beds} onClick={()=>void operate('housing')}>{i18n.language==='ru'?'Заселиться':'Move in'}</button>}</section>}
        {operationError&&<p className="error">{operationError}</p>}
        {target.kind==='building'&&ruler&&!village.structures.some(s=>s.plotId===target.plotId&&s.ownerAccountId)&&!village.storages.some(s=>s.plotId===target.plotId&&s.ownerAccountId)&&<div className="civic-actions"><button disabled={pending||!canUpgradeBuilding(village,target.plotId)} onClick={()=>void operate('upgrade')}>Улучшить</button>
          {confirm?<><span>Подтвердить медленную разборку?</span><button onClick={()=>void operate('demolish')}>Снести</button><button onClick={()=>setConfirm(false)}>Отмена</button></>:<button onClick={()=>setConfirm(true)}>Снести</button>}</div>}
        {site&&<SiteActions site={site} actorId={citizen.accountId} tsar={false} onAction={operation=>onCivic(site.plotId,operation)}/>}
        {blockedBank&&<p className="warning">Сначала постройте Царский мост.</p>}
        {working && <p className="small muted">{t('target.busy')}</p>}
        {error && <p className="error">{error}</p>}
        {wildWood && <p className="small muted">{t('target.wildWood')}</p>}
        {damage > 0 && (
          <p className="small warning">
            {t('building.damaged', { percent: Math.round((damage / catalog.rules.maximumDamage) * 100) })}
          </p>
        )}
        {site && (
          <SiteCard site={site} catalog={catalog} village={village} citizen={citizen} disabled={disabled} onStartJob={onStartJob} />
        )}
        {farmDenied&&<p className="small warning">{i18n.language==='ru'?'Для работы нужен допуск фермера. Обратитесь к царю или управляющему фермой.':'A farmer appointment is required. Ask the Tsar or farm manager.'}</p>}
        {addressedFarm?<FarmPlotBoard beds={beds} shared={!village.structures.find(s=>s.plotId===beds[0].plotId)?.ownerAccountId} now={now} raining={village.weather.raining} catalog={catalog} citizen={citizen} disabled={disabled||!!farmDenied} workers={workers} initialTaskId={initialFarmTaskId} onWork={(task,bed)=>onStart(task,null,bed.plotId,{plotId:bed.plotId,index:bed.index,generationId:bed.generationId!})}/>:beds.length>0&&<FieldsCard beds={beds} now={now} raining={village.weather.raining}/>}
        {addressedFarm&&slug&&<FarmHistory slug={slug} plotId={beds[0].plotId} version={beds.map(b=>`${b.generationId}:${b.contributions?.length??0}`).join('|')}/>}
        {storage?.ownerAccountId&&<p className="small muted">{i18n.language==='ru'?'Пополнить личный склад можно из личного имущества в кошельке.':'Deposit personal goods through your wallet’s private warehouse controls.'}</p>}
        {storage && (
          <StorageCard
            storage={storage}
            catalog={catalog}
            take={{ citizen, disabled, onTake: (resource, amount, sharpen, durability) => onTake(storage.plotId, resource, amount, sharpen, durability) }}
          />
        )}
        {tasks.some(task=>task.outputs.length>0)&&(target.kind==='building'||'spot' in target)&&<p className="small muted">{(() => {
          const area=village.kingdomArea??catalog.buildArea, tile='spot' in target?{x:Math.floor(target.spot.x),y:Math.floor(target.spot.y)}:{x:area.x,y:area.y}
          const privateBuilding=target.kind==='building'&&village.structures.some(s=>s.plotId===target.plotId&&s.ownerAccountId===citizen.accountId)
          const personal=privateBuilding||tile.x<area.x||tile.y<area.y||tile.x>=area.x+area.width||tile.y>=area.y+area.height
          return personal?(i18n.language==='ru'?'Добыча → личный запас':'Output → personal inventory'):(i18n.language==='ru'?'Добыча → общий запас':'Output → communal stock')
        })()}</p>}
        {tasks.length > 0 && (
          <TaskList
            tasks={tasks}
            citizen={citizen}
            catalog={catalog}
            built={built}
            discovered={discoveredTechnologies(village)}
            wildlife={village.wildlife}
            goldVein={village.goldVeinRemaining}
            fields={village.fields}
            resources={village.resources}
            disabled={disabled}
            onStart={(task) => onStart(task, spotFor(target, task), (target.kind === 'building'||target.kind==='site') && (task.requiredBuilding === target.building || task.effect === 'Repair' || task.id === CARRY_TO_STORAGE) ? target.plotId : undefined)}
          />
        )}
        {!site && !storage && !addressedFarm && tasks.length === 0 && <p className="small muted">{t('target.nothing')}</p>}
      </div>
    </div>
  )
}
