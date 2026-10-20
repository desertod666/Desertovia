import { usePopupGesture } from './usePopupGesture'
import { buildingNumber } from './buildingLabel'
import { api } from '../api'
import { SiteActions } from './SiteActions'
import type { ReactNode } from 'react'
import { CivicPanel } from './CivicPanel'
import type { Catalog, CitizenSummary, Site, SiteStage, VillageState } from '../api'
import type { WorldTarget } from '../game/worldMap'
import { useI18n } from '../i18n'
import { FieldsCard } from './FieldsCard'
import { FarmPlotBoard } from './FarmPlotBoard'
import { FarmHistory } from './FarmHistory'
import { ProjectPanel } from './ProjectPanel'
import { popupStyle } from './TargetPopup'
import { ResourceIcon } from './TileIcon'

/** Places the Tsar can click to watch: they never work, so the popup only shows how far the village got. */
export function watchable(target: WorldTarget, village: VillageState) {
  switch (target.kind) {
    case 'site':
    case 'project':
      return true
    case 'animal':
      return village.wildlife.some((herd) => herd.kind === target.prey)
    case 'building':
      return true
    default:
      return false
  }
}

function stageShare(stage: SiteStage) {
  const required = stage.jobs.reduce((sum, job) => sum + job.required, 0)
  const done = stage.jobs.reduce((sum, job) => sum + Math.min(job.done, job.required), 0)
  return required > 0 ? done / required : 0
}

/** Goods delivered and needed over every stage, per good. */
function siteMaterials(site: Site, catalog: Catalog) {
  const totals = new Map<string, { delivered: number; required: number }>()
  for (const stage of site.stages) {
    for (const job of stage.jobs) {
      const resource = catalog.jobs.find((j) => j.id === job.jobId && j.kind === 'Haul')?.resource
      if (!resource) continue
      const total = totals.get(resource) ?? { delivered: 0, required: 0 }
      total.delivered += Math.min(job.done, job.required)
      total.required += job.required
      totals.set(resource, total)
    }
  }
  return [...totals].map(([resource, total]) => ({ resource, ...total }))
}

function Row({ label, done, required }: { label: ReactNode; done: number; required: number }) {
  const i18n = useI18n()
  return (
    <div className="project-row">
      <span>{label}</span>
      <div className="bar thin">
        <div style={{ width: `${Math.min(100, (done / Math.max(required, 1e-6)) * 100)}%` }} />
      </div>
      <span className="numbers">
        {i18n.amount(done)} / {i18n.amount(required)}
      </span>
    </div>
  )
}

export function WatchPopup({
  target,
  screen,
  phone,
  village,
  catalog,
  citizens,
  now,
  onClose,
  actorId,
  onVillage,
  tsar,
  ruler,
}: {
  target: WorldTarget
  screen: { x: number; y: number }
  phone: boolean
  village: VillageState
  catalog: Catalog
  citizens: CitizenSummary[]
  now: number
  actorId:string
  onVillage:(state:VillageState)=>void
  tsar:boolean
  ruler:boolean
  onClose: () => void
}) {
  const gesture = usePopupGesture()
  const i18n = useI18n()
  const { t } = i18n
  const site = target.kind === 'site' ? village.sites.find((s) => s.plotId === target.plotId) : undefined
  const beds = target.kind === 'building' ? village.fields.filter((bed) => bed.plotId === target.plotId) : []
  const addressedFarm=beds.length>0&&beds.every(b=>!!b.generationId)
  const herd = target.kind === 'animal' ? village.wildlife.find((h) => h.kind === target.prey) : undefined
  const title =
    target.kind === 'project'
      ? i18n.project(target.projectId).name
      : target.kind === 'site' || target.kind === 'building'
        ? `${i18n.building(target.building).name} ${buildingNumber(village,target.plotId)}`
        : target.kind === 'animal'
          ? i18n.animal(target.prey)
          : ''

  return (
    <div {...gesture} className={`target-popup ${phone ? 'sheet' : ''} ${addressedFarm?'farm-popup':''}`} style={popupStyle(screen, phone,addressedFarm?520:340,addressedFarm?600:undefined)} role="dialog" aria-label={title}>
      <header className="target-popup-header">
        <strong>{title}</strong>
        <button className="close" onClick={onClose} title={`${t('close')} (Esc)`}>
          ✕
        </button>
      </header>
      <div className="target-popup-body">
        {site&&<SiteActions site={site} actorId={actorId} tsar={tsar} onAction={async operation=>onVillage(await api.civic(village.slug,site.plotId,operation))}/>}
        {target.kind==='building'&&ruler&&<CivicPanel village={village} tsar={tsar} ruler={ruler} actorId={actorId} onVillage={onVillage} plotId={target.plotId} />}
        {target.kind === 'site' &&
          (site ? (
            <SiteProgress site={site} catalog={catalog} citizens={citizens} />
          ) : (
            <p className="small highlight">{t('site.complete')}</p>
          ))}
        {target.kind === 'project' && <ProjectPanel project={village.project} completed={village.completedProjects} />}
        {addressedFarm?<><FarmPlotBoard beds={beds} shared={!village.structures.find(s=>s.plotId===beds[0].plotId)?.ownerAccountId} now={now} raining={village.weather.raining} catalog={catalog} workers={citizens}/><FarmHistory slug={village.slug} plotId={beds[0].plotId} version={beds.map(b=>`${b.generationId}:${b.contributions?.length??0}`).join('|')}/></>:beds.length>0&&<FieldsCard beds={beds} now={now} raining={village.weather.raining}/>}
        {herd && <p className="small">{t('watch.herd', { count: herd.count, capacity: herd.capacity })}</p>}
      </div>
    </div>
  )
}

function SiteProgress({ site, catalog, citizens }: { site: Site; catalog: Catalog; citizens: CitizenSummary[] }) {
  const i18n = useI18n()
  const { t } = i18n
  const working = citizens.filter((citizen) => citizen.siteId === site.plotId && citizen.state === 'Working')
  const reserved = site.stages.some(stage=>stage.jobs.some(job=>(job.reserved??0)>0))
  const current = site.stages[site.stage]
  const materials = siteMaterials(site, catalog)

  return (
    <>
      <div className="site-head">
        <span>{Math.floor(site.progress * 100)}%</span>
        {current && (
          <span className={`small ${current.hands >= current.crewNeeded ? 'highlight' : ''}`} title={t('site.crewHint', { needed: current.crewNeeded })}>
            {t('site.hands', { hands: current.hands, needed: current.crewNeeded })}
          </span>
        )}
      </div>
      <div className="bar gold">
        <div style={{ width: `${Math.floor(site.progress * 100)}%` }} />
      </div>
      {materials.length > 0 && (
        <>
          <h4 className="muted small">{t('watch.materials')}</h4>
          <div className="project-rows">
            {materials.map((material) => (
              <Row
                key={material.resource}
                label={
                  <>
                    <ResourceIcon kind={material.resource} scale={1} /> {i18n.resource(material.resource)}
                  </>
                }
                done={material.delivered}
                required={material.required}
              />
            ))}
          </div>
        </>
      )}
      <ol className="watch-stages">
        {site.stages.map((stage, index) => {
          const share = index < site.stage ? 1 : stageShare(stage)
          return (
            <li key={stage.id} className={index === site.stage ? 'current' : index < site.stage ? 'done' : 'muted'}>
              <span className="small">
                {i18n.stage(stage.id)}
                {index < site.stage && <span className="highlight"> ✓ {t('site.doneMark')}</span>}
              </span>
              <div className="bar thin">
                <div style={{ width: `${Math.floor(share * 100)}%` }} />
              </div>
              {index === site.stage && (
                <div className="project-rows">
                  {stage.jobs.map((job) => (
                    <Row key={job.jobId} label={i18n.job(job.jobId)} done={job.done} required={job.required} />
                  ))}
                </div>
              )}
            </li>
          )
        })}
      </ol>
      {current && stageShare(current) >= 1 - 1e-6 && current.hands < current.crewNeeded && (
        <p className="small highlight">{t('site.handsWanted')}</p>
      )}
      <p className="small">
        {working.length > 0 ? t('watch.working', { names: working.map((citizen) => citizen.name).join(', ') }) : reserved ? (i18n.language==='ru'?'Работа назначена. Ожидаем завершения поручений.':'Work is assigned. Waiting for the orders to finish.') : t('watch.nobody')}
      </p>
    </>
  )
}
