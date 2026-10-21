import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import {
  api,
  dungeonPath,
  myHero,
  type Catalog,
  type Citizen,
  type DungeonRun,
  type HeroClass,
  type HeroClassOffer,
  type VillageState,
} from '../api'
import { useI18n } from '../i18n'
import './royalDungeon.css'

const GATHERING_SECONDS = 60

function countdown(milliseconds: number) {
  const seconds = Math.max(0, Math.ceil(milliseconds / 1000))
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`
}

/** The run under way in the village: the gathering countdown, the march, the fight, or how it ended. */
export function DungeonBanner({
  slug,
  run,
  now,
  canAnswer,
  onAnswer,
}: {
  slug: string
  run: DungeonRun
  now: number
  /** The player's citizen may still answer the call. */
  canAnswer: boolean
  onAnswer: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const names = run.heroes.map((hero) => hero.name).join(', ')
  let title: string
  if (run.phase === 'Gathering') title = t('dungeon.banner.gathering', { time: countdown(Date.parse(run.gatheringEndsAt) - now) })
  else if (run.phase === 'Marching') title = t('dungeon.banner.marching')
  else if (run.phase === 'Running') title = t('dungeon.banner.running', { slain: run.slain, foes: run.foes })
  else title = t(`dungeon.banner.${run.outcome === 'Pending' ? 'Abandoned' : run.outcome}`)
  const watchable = run.phase === 'Marching' || run.phase === 'Running'
  return (
    <div className="raid-banner dungeon-banner" role="status">
      <div className="raid-banner-title">{title}</div>
      <div className="raid-banner-row small">
        <span>{t('dungeon.heroes', { names })}</span>
        {run.phase === 'Gathering' && canAnswer && (
          <button className="primary" onClick={onAnswer}>
            {t('dungeon.answer')}
          </button>
        )}
        {watchable && (
          <Link className="button" to={dungeonPath(slug, run.id)}>
            {t('dungeon.watch')}
          </Link>
        )}
      </div>
    </div>
  )
}

/** The Tsar's call to the Dead Lands. */
export function DungeonPanel({
  village,
  pending,
  error,
  onOpen,
  onCancel,
  onVillage,
}: {
  village: VillageState
  pending: boolean
  error: string | null
  onOpen: () => void
  onCancel: () => void
  onVillage?: (next: VillageState) => void
}) {
  const { t } = useI18n()
  const run = village.dungeon
  const active = run !== null && run.phase !== 'Ended'
  return (
    <section className="card">
      <h3>{t('dungeon.tsarTitle')}</h3>
      <p className="small">{t(active&&!run.combatRulesVersion?'dungeon.tsarHint':'dungeon.mortalTsarHint', { seconds: GATHERING_SECONDS })}</p>
      {active&&run.phase==='Gathering'&&run.combatRulesVersion===1&&onVillage&&<RoyalDungeonClassPicker village={village} onVillage={onVillage}/>}
      {!active ? (
        <button className="primary" disabled={pending} onClick={onOpen}>
          {t('dungeon.open')}
        </button>
      ) : run.phase === 'Gathering' ? (
        <button disabled={pending} onClick={onCancel}>
          {t('dungeon.cancel')}
        </button>
      ) : null}
      {error && <p className="error">{error}</p>}
    </section>
  )
}

/** Functional class selection in the existing menu; graphic redesign is deferred by D-109. */
export function RoyalDungeonClassPicker({village,onVillage}:{village:VillageState;onVillage:(next:VillageState)=>void}) {
  const i18n=useI18n(),{t}=i18n,run=village.dungeon
  const current=run?.heroes.find(h=>h.citizenId===null)?.combatClass??'Brawler'
  const [offers,setOffers]=useState<HeroClassOffer[]|null>(null),[selected,select]=useState<HeroClass>(current)
  const [pending,setPending]=useState(false),[error,setError]=useState<string|null>(null)
  useEffect(()=>{
    let cancelled=false
    setOffers(null);select(current);setError(null)
    api.royalHeroClasses(village.slug).then(value=>{if(!cancelled)setOffers(value)},exception=>{if(!cancelled)setError(i18n.error(exception))})
    return ()=>{cancelled=true}
  },[village.slug,run?.id,current,i18n])
  const offer=offers?.find(o=>o.class===selected)
  return <form className="royal-dungeon-class" onSubmit={async event=>{
    event.preventDefault();if(pending||!offer||selected===current)return
    setPending(true);setError(null)
    try{onVillage(await api.pickRoyalHeroClass(village.slug,selected))}catch(exception){setError(i18n.error(exception))}finally{setPending(false)}
  }}>
    <p className="small">{t('dungeon.joined',{heroClass:i18n.heroClass(current)})}</p>
    <div className="row"><label>{t('dungeon.pickClass')} <select aria-label={t('dungeon.pickClass')} value={selected} disabled={pending||!offers} onChange={e=>select(e.target.value as HeroClass)}>
      {!offers&&<option value={current}>{t('loading')}</option>}
      {offers?.map(o=><option key={o.class} value={o.class}>{i18n.heroClass(o.class)}</option>)}
    </select></label><button disabled={pending||!offer||selected===current}>{pending?t('loading'):t('dungeon.accept')}</button></div>
    {offer&&<p className="small">{t('dungeon.classStats',{health:offer.health,damage:offer.damage,range:offer.range})}</p>}
    {offer&&(offer.weapon||offer.armor)&&<p className="small">{t('dungeon.classGear',{gear:[offer.weapon,offer.armor].filter((kind):kind is string=>kind!==null).map(kind=>i18n.resource(kind)).join(', ')})}</p>}
    {error&&<p className="error" role="alert">{error}</p>}
  </form>
}

/**
 * The call to the dungeon for a citizen: answering opens the class picker; closing declines. Shown again to switch
 * class or stay home while the gathering lasts.
 */
export function DungeonInvite({
  slug,
  run,
  now,
  citizen,
  catalog,
  onCitizen,
  onClose,
}: {
  slug: string
  run: DungeonRun
  now: number
  citizen: Citizen
  catalog: Catalog
  onCitizen: (next: Citizen) => void
  onClose: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const hero = myHero(run, citizen.id, false)
  const [picking, setPicking] = useState(false)
  const [offers, setOffers] = useState<HeroClassOffer[] | null>(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!picking) return
    let cancelled = false
    api.heroClasses(slug).then(
      (next) => !cancelled && setOffers(next),
      (exception) => !cancelled && setError(i18n.error(exception)),
    )
    return () => {
      cancelled = true
    }
  }, [picking, slug, i18n])

  const pick = () => {
    setOffers(null)
    setError(null)
    setPicking(true)
  }

  const act = async (action: () => Promise<Citizen>, after?: () => void) => {
    setPending(true)
    setError(null)
    try {
      onCitizen(await action())
      after?.()
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  const gear = (offer: HeroClassOffer) =>
    [offer.weapon, offer.armor].filter((kind): kind is string => kind !== null).map((kind) => i18n.resource(kind))
  const busy = citizen.state === 'Working'
  const classes = catalog.heroClasses.filter((definition) => definition.class !== 'Voivode')

  return (
    <div className="modal-backdrop" onPointerDown={(event) => event.target === event.currentTarget && onClose()}>
      <div className="modal dungeon-invite" role="dialog" aria-label={t('dungeon.inviteTitle')}>
        <header className="modal-header">
          <h2>{t('dungeon.inviteTitle')}</h2>
          <button className="close" onClick={onClose} title={`${t('close')} (Esc)`}>
            ✕
          </button>
        </header>
        <div className="modal-body">
          <p className="small">{t('dungeon.inviteBody')}</p>
          <p>
            <strong>{t('dungeon.countdown', { time: countdown(Date.parse(run.gatheringEndsAt) - now) })}</strong>
          </p>
          {hero && !picking && <p>{t('dungeon.joined', { heroClass: i18n.heroClass(hero.class) })}</p>}
          {!picking ? (
            <div className="row">
              {hero ? (
                <>
                  <button disabled={pending} onClick={pick}>
                    {t('dungeon.pickClass')}
                  </button>
                  <button disabled={pending} onClick={() => act(() => api.leaveDungeon(slug))}>
                    {t('dungeon.leave')}
                  </button>
                </>
              ) : (
                <>
                  <button className="primary" disabled={pending} onClick={pick}>
                    {t('dungeon.accept')}
                  </button>
                  <button disabled={pending} onClick={onClose}>
                    {t('dungeon.decline')}
                  </button>
                </>
              )}
            </div>
          ) : (
            <>
              <h3>{t('dungeon.pickClass')}</h3>
              {busy && !hero && <p className="small muted">{t('dungeon.cancelsTask')}</p>}
              {offers === null && !error && <p className="muted">{t('loading')}</p>}
              {offers && (
                <div className="task-grid">
                  {classes.map((definition) => {
                    const offer = offers.find((candidate) => candidate.class === definition.class)
                    const current = hero?.class === definition.class
                    return (
                      <div key={definition.class} className={`task-card ${current ? 'current' : ''} ${offer ? '' : 'locked'}`}>
                        <span className="task-text">
                          <span className="task-name">{i18n.heroClass(definition.class)}</span>
                          <span className="task-meta">{i18n.heroClassHint(definition.class)}</span>
                          {offer && (
                            <span className="task-meta">
                              {t('dungeon.classStats', { health: offer.health, damage: offer.damage, range: offer.range })}
                            </span>
                          )}
                          {offer && gear(offer).length > 0 && (
                            <span className="task-meta">{t('dungeon.classGear', { gear: gear(offer).join(', ') })}</span>
                          )}
                          {offer && !current && (
                            <span className="task-actions">
                              <button
                                className="small-button primary"
                                disabled={pending}
                                onClick={() =>
                                  act(
                                    () => api.joinDungeon(slug, definition.class as HeroClass),
                                    () => setPicking(false),
                                  )
                                }
                              >
                                {t('dungeon.accept')}
                              </button>
                            </span>
                          )}
                        </span>
                      </div>
                    )
                  })}
                </div>
              )}
            </>
          )}
          {error && <p className="error">{error}</p>}
        </div>
      </div>
    </div>
  )
}
