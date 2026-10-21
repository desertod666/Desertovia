import { MonasteryPanel } from '../ui/MonasteryPanel'
import { WalletDialog, WalletHud } from '../ui/WalletHud'
import { PersonalBattlePanel } from '../ui/PersonalBattlePanel'
import {actorFighter} from '../game/battleActor'
import { SafeRestControl } from '../ui/SafeRestControl'
import {WorldAtlas} from '../ui/WorldAtlas'
import {JourneyControl} from '../ui/JourneyControl'
import { createOperationId,type FarmWorkTarget } from '../api'
import { HospitalPanel } from '../ui/HospitalPanel'
import { RescuePatientPanel } from '../ui/RescuePatientPanel'
import { SramoslavPanel } from '../ui/SramoslavPanel'
import { useReportContext } from '../reports/context'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import {
  api,
  ApiError,
  CARRY_TO_STORAGE,
  comingRaids,
  fightingCitizenIds,
  dungeonPath,
  myArrival,
  myHero,
  INVITE_PARAMETER,
  isStoryQuest,
  takesSpot,
  type BuildingDefinition,
  type Catalog,
  type Citizen,
  type CycleReport,
  type Me,
  type Task,
  type TilePoint,
  type VillageState,
} from '../api'
import type { JobChoice } from '../ui/SitesPanel'
import { inForestArea, tileBuiltOver, type WorldTarget } from '../game/worldMap'
import { TargetPopup } from '../ui/TargetPopup'
import { watchable, WatchPopup } from '../ui/WatchPopup'
import { flagTitle, NotificationProvider, useGameNotifications, useTitleReset } from '../notifications'
import { BackgroundMusic } from '../ui/BackgroundMusic'
import { villageMusicScene } from '../game/musicContext'
import { GameCanvas, type GameCanvasHandle, type Placement } from '../game/GameCanvas'
import { HandsCard } from '../ui/HandsCard'
import { PRIEST_ID, TSAR_ID } from '../game/VillageWorld'
import { useLiveVillage, useMediaQuery, useServerNow } from '../hooks'
import { onAppResume } from '../telegram'
import { GameMenu } from '../ui/GameMenu'
import type { MenuTab } from '../ui/menuTabs'
import { BottomBar, TopBar } from '../ui/Hud'
import { ChatPanel } from '../ui/ChatPanel'
import { InventoryPanel } from '../ui/InventoryPanel'
import { useI18n } from '../i18n'
import { TaskDonePanel } from '../ui/TaskDonePanel'
import { Guide } from '../ui/Guide'
import { QuestTracker } from '../ui/QuestPanel'
import { CitizenDialog } from '../ui/CitizenDialog'
import { PollPanel, PollSetup } from '../ui/PollPanel'
import { ServerClock } from '../ui/ServerClock'
import { LoginPanel } from '../ui/LoginPanel'
import { CreateCharacterForm } from '../ui/CreateCharacterForm'
import { RaidBanner } from '../ui/RaidBanner'
import { DungeonBanner, DungeonInvite } from '../ui/Dungeon'
import { declinedDungeon, declineDungeon } from '../ui/dungeonInvites'

type Panel = 'none' | 'menu' | 'inventory' | 'done'

const ZOOM_STEP = 1.25
/** Results of a closed vote pop up for players who come within this time. */
const POLL_RESULTS_MS = 10 * 60 * 1000
const POLL_CLOCK_MS = 15_000
const RAID_CLOCK_MS = 5_000
const seenPollKey = (slug: string) => `desertod.poll-seen.${slug}`
/** The village page sends the player to a run once; coming back from it leaves them in the village. */
const sentToDungeonKey = (runId: string) => `desertod.dungeon-sent.${runId}`
const UI_HIDDEN_KEY = 'desertod.ui-hidden'
/** Long enough to read a chat line over the author's head: 4 s plus about one second per 15 characters. */
const chatBubbleSeconds = (text: string) => Math.min(12, 4 + text.length / 15)

/** Villages are for signed-in players only: visitors sign in before the map loads. */
export function GamePage({primarySlug}:{primarySlug?:string}={}) {
  const { t } = useI18n()
  const [me, setMe] = useState<Me | null | undefined>(undefined)
  const loadMe = useCallback(async () => setMe(await api.me()), [])

  useEffect(() => {
    api.me().then(setMe)
  }, [])

  if (me === undefined) return <div className="splash">{t('loading')}</div>
  if (!me) {
    return (
      <div className="menu-screen">
        <LoginPanel onLoggedIn={loadMe} />
        <Link to="/">{t('toHome')}</Link>
      </div>
    )
  }
  if (!me.character) {
    return (
      <div className="menu-screen">
        <CreateCharacterForm suggestedName={me.displayName} onCreated={setMe} />
      </div>
    )
  }
  return <NotificationProvider key={me.id} accountId={me.id}><VillageView key={me.id} me={me} onAccountChanged={loadMe} primarySlug={primarySlug} /></NotificationProvider>
}

function VillageView({ me, onAccountChanged, primarySlug }: { me: Me; onAccountChanged: () => void; primarySlug?:string }) {
  const { slug:routeSlug } = useParams()
  const slug=routeSlug??primarySlug??''
  const navigate = useNavigate()
  const i18n = useI18n()
  const [catalog, setCatalog] = useState<Catalog | null>(null)
  const [ownCitizen, setCitizen] = useState<Citizen | null | undefined>(undefined)
  const [walletOpen,setWalletOpen]=useState(false)
  const [monasteryOpen,setMonasteryOpen]=useState(false)
  const [placing, setPlacing] = useState<{ definition: BuildingDefinition; private?:boolean; requestId?:string } | null>(null)
  /** Citizen the Tsar clicked: appoints them Governor or writes their subtitle. */
  const [subtitleFor, setSubtitleFor] = useState<string | null>(null)
  /** Place on the map the player clicked to send their citizen there. */
  const [picked, setPicked] = useState<{ target: WorldTarget; screen: { x: number; y: number };farmTaskId?:string } | null>(null)
  const [atlasOpen,setAtlasOpen]=useState(false)
  const [enemyPick,setEnemyPick]=useState<{battleId:string;unitId:string;selectionToken:number}|null>(null)
  const enemySelection=useRef(0)
  const [notice, setNotice] = useState<string | null>(null)
  const [serverOffset, setServerOffset] = useState(0)
  const {notify,cancel:cancelNotification,now:notificationNow}=useGameNotifications(slug)
  const [patientFor,setPatientFor]=useState<string|null>(null)
  const [hospitalPatient,setHospitalPatient]=useState<string|null>(null)
  const [hospitalOpen, setHospitalOpen] = useState(false)
  const [chaplainOpen, setChaplainOpen] = useState(false)
  const [chatFull, setChatFull] = useState(false)
  const [panel, setPanel] = useState<Panel>('none')
  const [tab, setTab] = useState<MenuTab>('village')
  const [report, setReport] = useState<CycleReport | null>(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  /** The Tsar or a Governor is ticking discoveries for a new vote. */
  const [pollSetup, setPollSetup] = useState(false)
  /** Open vote the player hid; the next one pops up again. */
  const [hiddenPoll, setHiddenPoll] = useState<string | null>(null)
  /** Closed vote whose results the player has seen. */
  const [seenPoll, setSeenPoll] = useState(() => localStorage.getItem(seenPollKey(slug)))
  const [pollError, setPollError] = useState<string | null>(null)
  /** Development only: the day-night toggle forces daylight or night; null follows the village sky. */
  const [nightOverride, setNightOverride] = useState<boolean | null>(null)
  /** Everything but the map and the popups of clicked objects is hidden. */
  const [uiHidden, setUiHidden] = useState(() => localStorage.getItem(UI_HIDDEN_KEY) === '1')
  const toggleUi = () => {
    const hidden = !uiHidden
    localStorage.setItem(UI_HIDDEN_KEY, hidden ? '1' : '0')
    setUiHidden(hidden)
    if (hidden) setPanel((current) => (current === 'menu' || current === 'inventory' ? 'none' : current))
  }
  const phone = useMediaQuery('(max-width: 720px)')
  const pollClock = useServerNow(serverOffset, POLL_CLOCK_MS)
  useReportContext(() => ({ slug, page: 'village',
    menu: [panel === 'menu' ? `menu:${tab}` : panel, walletOpen && 'wallet', monasteryOpen && 'monastery',
      hospitalOpen && 'hospital', (hospitalPatient || patientFor) && 'patient', chaplainOpen && 'chaplain',
      placing && 'placement', subtitleFor && 'citizen'].filter(Boolean).join(',').slice(0, 80),
    selection: picked?.target ? JSON.stringify(picked.target).slice(0, 160) :
      enemyPick ? `enemy:${enemyPick.battleId}:${enemyPick.unitId}` : subtitleFor ?? hospitalPatient ?? patientFor ?? undefined,
    x: ownCitizen?.spot?.x, y: ownCitizen?.spot?.y }))
  const canvas = useRef<GameCanvasHandle>(null)
  const citizenId = useRef<string | null>(null)
  const lastCitizenSnapshot = useRef(-Infinity)
  const taskStartRequests=useRef(new Map<string,string>())
  const applyCitizen = useCallback((next: Citizen | null) => {
    if(next && Date.parse(next.serverNow)<lastCitizenSnapshot.current)return
    lastCitizenSnapshot.current=next?Date.parse(next.serverNow):-Infinity
    setCitizen(next)
    if (next) setServerOffset(Date.parse(next.serverNow) - Date.now())
  }, [])

  const loadCitizen = useCallback(async () => applyCitizen(await api.myCitizen(slug)), [slug, applyCitizen])
  useEffect(() => {
    const refresh = () => { if (document.visibilityState !== 'hidden') void loadCitizen().catch(() => {}) }
    const interval = window.setInterval(refresh, 30_000)
    const stopResume = onAppResume(() => { void loadCitizen().catch(() => {}) })
    return () => { window.clearInterval(interval); stopResume() }
  }, [loadCitizen])

  useEffect(() => {
    lastCitizenSnapshot.current=-Infinity
    api.myCitizen(slug).then(applyCitizen)
    api.catalog().then(setCatalog)
  }, [slug, applyCitizen])

  const titleRef = useRef('Desertovia')
  const catalogRef = useRef<Catalog | null>(null)

  const live = useLiveVillage(slug, !!ownCitizen, {
    onCitizenUpdated: (summary) => {
      if (summary.id === citizenId.current) void loadCitizen()
    },
    onCycleCompleted: (finished,at) => {
      const occurredAt=Date.parse(finished.settledAt??at??'')
      const cycleKey=`${finished.citizenId}:${finished.cycleStartedAt??at??''}`
      canvas.current?.say(finished.citizenId, i18n.speechFor(finished))
      if (finished.buildingCompleted) {
        const building = i18n.building(finished.buildingCompleted).name
        notify(i18n.t('notify.builtTitle', { building }), i18n.t('notify.builtBody'), `built:${finished.siteId??building}:${cycleKey}`,'building',occurredAt,occurredAt+3600000)
        flagTitle('★', titleRef.current)
      }
      if (finished.technologyDiscovered && finished.technologyId) {
        const technology = i18n.technology(finished.technologyId).name
        notify(
          i18n.t('notify.discoveredTitle', { technology }),
          i18n.t('notify.discoveredBody'),
          `discovered-${finished.technologyId}`,'building',occurredAt,occurredAt+3600000,
        )
        flagTitle('★', titleRef.current)
      }
      if (finished.citizenId === citizenId.current) {
        setReport(finished)
        setError(null)
        setPanel((current) => (current === 'none' ? 'done' : current))
        if (!finished.buildingCompleted && !finished.technologyDiscovered) {
          notify(
            i18n.t('notify.doneTitle', { task: i18n.task(finished.taskId) }),
            i18n.t('notify.doneBody', { seconds: catalogRef.current?.rules.graceSeconds ?? 60 }),
            `cycle:${cycleKey}`,'work',occurredAt,occurredAt+300000,
          )
        }
        flagTitle('✓', titleRef.current)
      }
    },
    onProjectCompleted: (completion,at) => {
      const occurredAt=Date.parse(at??'')
      const project = i18n.project(completion.projectId).name
      canvas.current?.say(PRIEST_ID, i18n.t('priest.completed', { project }))
      notify(i18n.t('notify.builtTitle', { building: project }), i18n.t('notify.builtBody'), `project-${completion.projectId}`,'building',occurredAt,occurredAt+3600000)
      flagTitle('★', titleRef.current)
    },
    onChatMessage: (message) => {
      canvas.current?.say(message.isChaplain ? PRIEST_ID : message.citizenId ?? TSAR_ID, message.text, chatBubbleSeconds(message.text))
    },
    onRaidFought: (fought,at) => {
      const occurredAt=Date.parse(at??'')
      cancelNotification(`raid-coming-${fought.raidId}`)
      const line = i18n.describeRaid(fought)
      setNotice(line)
      window.setTimeout(() => setNotice(null), 8000)
      notify(i18n.t('notify.raidOverTitle'), line, `raid-${fought.raidId}`,'danger',occurredAt,occurredAt+600000)
      flagTitle(fought.repelled ? '★' : '!', titleRef.current)
    },
    onDungeonEnded: (run) => {
      cancelNotification(`dungeon-${run.id}`)
      if (run.outcome === 'Cancelled') return
      const occurredAt=Date.parse(run.endedAt??'')
      notify(i18n.t('notify.dungeonTitle'),i18n.describeDungeon(run),`dungeon-ended:${run.id}`,'adventure',occurredAt,occurredAt+1800000)
      setNotice(i18n.describeDungeon(run))
      window.setTimeout(() => setNotice(null), 8000)
    },
    onQuestCompleted: (completion,at) => {
      const occurredAt=Date.parse(at??'')
      const done = i18n.t(isStoryQuest(completion.kind) ? 'quest.storyDone' : 'quest.done', {
        quest: i18n.quest(completion).title,
      })
      const renown = citizenId.current ? completion.renownByCitizen[citizenId.current] : undefined
      setNotice(renown ? `${done} ${i18n.t('quest.renown', { renown: i18n.amount(renown) })}` : done)
      window.setTimeout(() => setNotice(null), 6000)
      notify(done, i18n.t('notify.questBody'), `quest-${completion.questId}`,'work',occurredAt,occurredAt+1800000)
      flagTitle('★', titleRef.current)
    },
    onVillageDeleted: () => navigate('/'),
  })

  const battleEventsSeen=useRef(new Map<string,number>())
  useEffect(()=>{
    for(const battle of live.state?.battles??[]){
      const seen=battleEventsSeen.current.get(battle.id)??0
      const fresh=battle.events.filter(e=>e.sequence>seen&&Date.now()+serverOffset-Date.parse(e.at)<5000)
      battleEventsSeen.current.set(battle.id,Math.max(seen,...battle.events.map(e=>e.sequence)))
      if(fresh.some(e=>e.kind==='breach')){
        setNotice(i18n.language==='ru'?'Пролом в заборе! Враги могут пройти внутрь.':'The palisade has been breached!')
        window.setTimeout(()=>setNotice(null),6000)
      }
    }
  },[live.state?.battles,serverOffset,i18n.language])

  const brand = i18n.t('brand')
  const title = live.state ? `${live.state.name} — ${brand}` : brand
  useEffect(() => {
    titleRef.current = title
    catalogRef.current = catalog
  }, [title, catalog])
  useTitleReset(title)


  // The village owner is the Tsar: rules and plans, never works. Governors are citizens the Tsar lets plan too.
  const tsar = live.isOwner
  const citizen = tsar ? null : ownCitizen
  const observedCrops=useRef(new Map<string,{generation:string;ripe:boolean}>())
  useEffect(()=>{
    if(!live.state)return
    const present=new Set<string>()
    for(const bed of live.state.fields){
      const key=`${bed.plotId}:${bed.index}`,generation=bed.generationId??'',previous=observedCrops.current.get(key)
      present.add(key)
      if(previous?.generation&&(previous.generation!==generation||!bed.crop))cancelNotification(`farm-ripe:${previous.generation}`)
      const ripe=!!bed.crop&&bed.growth>=1
      const farm=live.state.structures.find(s=>s.plotId===bed.plotId&&s.kind==='Farm')
      const ownCare=bed.contributions?.some(c=>c.accountId===me.id)??false
      const allowed=farm&&(!farm.ownerAccountId||farm.ownerAccountId===me.id)&&(tsar||!!citizen?.isFarmer||ownCare)
      if(allowed&&generation&&previous?.generation===generation&&!previous.ripe&&ripe&&bed.crop){
        const at=notificationNow(),crop=i18n.crop(bed.crop)
        notify(i18n.language==='ru'?`${crop}: урожай готов`:`${crop}: harvest ready`,i18n.t('field.bed',{number:bed.index+1}),`farm-ripe:${generation}`,'farm',at,at+6*3600000)
      }
      observedCrops.current.set(key,{generation,ripe})
    }
    for(const [key,crop] of observedCrops.current)if(!present.has(key)){cancelNotification(`farm-ripe:${crop.generation}`);observedCrops.current.delete(key)}
  },[live.state,tsar,citizen?.isFarmer,me.id,i18n,notify,cancelNotification,notificationNow])
  const ruler = tsar || citizen?.isGovernor === true

  const [joining, setJoining] = useState(false)
  const [joinError, setJoinError] = useState<string | null>(null)
  const join = useCallback(
    async (inviteCode: string | null) => {
      setJoining(true)
      setJoinError(null)
      try {
        applyCitizen(await api.joinVillage(slug, inviteCode))
      } catch (exception) {
        // The session may belong to another account than the one this tab loaded.
        if (exception instanceof ApiError && exception.code === 'character.missing') {
          onAccountChanged()
          return
        }
        setJoinError(i18n.error(exception))
      } finally {
        setJoining(false)
      }
    },
    [slug, applyCitizen, i18n, onAccountChanged],
  )
  // The account's character settles in on the first visit; a private village waits for the invite code.
  const triedJoin = useRef(false)
  const isPrivate = live.state?.isPrivate
  const linkInvite = new URLSearchParams(window.location.search).get(INVITE_PARAMETER)
  useEffect(() => {
    if (triedJoin.current || ownCitizen !== null || isPrivate === undefined || tsar) return
    if (isPrivate && !linkInvite) return
    triedJoin.current = true
    void join(isPrivate ? linkInvite : null)
  }, [ownCitizen, isPrivate, tsar, linkInvite, join])
  const placingRef = useRef(placing)
  const subtitleForRef = useRef(subtitleFor)
  const pickedRef = useRef(picked)
  /** Closes the vote screen when it is shown. */
  const closePollRef = useRef<(() => void) | null>(null)
  useEffect(() => {
    citizenId.current = citizen?.id ?? null
    placingRef.current = placing
    subtitleForRef.current = subtitleFor
    pickedRef.current = picked
  })

  // Calls everyone to the vote when it opens, and shares the result when it closes.
  const lastPoll = useRef<{ id: string; open: boolean } | null | undefined>(undefined)
  const poll = live.state?.poll ?? null
  useEffect(() => {
    if (!live.state) return
    const previous = lastPoll.current
    lastPoll.current = poll ? { id: poll.id, open: poll.closedAt === null } : null
    if(previous?.open&&(!poll||poll.id!==previous.id||poll.closedAt!==null))cancelNotification(`poll-open:${previous.id}`)
    if (previous === undefined || !poll) return
    if (poll.closedAt === null && previous?.id !== poll.id) {
      const occurredAt=Date.parse(poll.openedAt)
      notify(i18n.t('notify.pollTitle'), i18n.t('notify.pollBody'), `poll-open:${poll.id}`,'civic',occurredAt,occurredAt+600000)
      flagTitle('?', titleRef.current)
    } else if (poll.winner && previous?.id === poll.id && previous.open) {
      cancelNotification(`poll-open:${poll.id}`)
      const occurredAt=Date.parse(poll.closedAt??'')
      notify(
        i18n.t('notify.pollClosedTitle', { technology: i18n.technology(poll.winner).name }),
        i18n.t('notify.pollClosedBody'),
        `poll-closed:${poll.id}`,'civic',occurredAt,occurredAt+600000,
      )
      flagTitle('★', titleRef.current)
    }
  }, [live.state, poll, i18n])

  // Warns everyone once when an attack comes into sight.
  const raidClock = useServerNow(serverOffset, RAID_CLOCK_MS)
  const warnedRaids = useRef(new Set<string>())
  useEffect(() => {
    if (!live.state) return
    for(const raid of live.state.raids)if(raid.resolvedAt)cancelNotification(`raid-coming-${raid.id}`)
    for (const raid of comingRaids(live.state.raids, raidClock)) {
      if (warnedRaids.current.has(raid.id)) continue
      warnedRaids.current.add(raid.id)
      notify(
        i18n.t('notify.raidTitle', { enemy: i18n.enemy(raid.enemy) }),
        i18n.t('notify.raidBody', { minutes: Math.max(1, Math.ceil((Date.parse(raid.strikesAt) - raidClock) / 60_000)) }),
        `raid-coming-${raid.id}`,'danger',Date.parse(raid.announcedAt),Date.parse(raid.strikesAt),
      )
      flagTitle('!', titleRef.current)
    }
  }, [live.state, raidClock, i18n])

  // The Tsar's call to a dungeon pops up for every citizen once; closing it declines.
  const dungeon = live.state?.dungeon ?? null
  const [inviteFor, setInviteFor] = useState<string | null>(null)
  const invitedRuns = useRef(new Set<string>())
  const citizenReady = !!citizen && (!citizen.restState || citizen.restState === 'Active')
  useEffect(() => {
    for(const id of invitedRuns.current)if(id!==dungeon?.id||dungeon.phase!=='Gathering'||myHero(dungeon,citizen?.id??null,tsar))cancelNotification(`dungeon-${id}`)
    if (!dungeon || dungeon.phase !== 'Gathering' || !citizenReady) return
    if (invitedRuns.current.has(dungeon.id)) return
    invitedRuns.current.add(dungeon.id)
    if (declinedDungeon(dungeon.id)) return
    setInviteFor(dungeon.id)
    notify(i18n.t('notify.dungeonTitle'), i18n.t('notify.dungeonBody', { seconds: 60 }), `dungeon-${dungeon.id}`,'adventure',Date.parse(dungeon.openedAt),Date.parse(dungeon.gatheringEndsAt))
    flagTitle('!', titleRef.current)
  }, [dungeon, citizenReady, i18n])
  const closeInvite = () => {
    if(inviteFor)cancelNotification(`dungeon-${inviteFor}`)
    if (inviteFor && dungeon && !myHero(dungeon, citizen?.id ?? null, false)) declineDungeon(inviteFor)
    setInviteFor(null)
  }

  // Heroes go to the run's page once their figure reaches the edge of the land.
  const arrival = dungeon && (dungeon.phase === 'Marching' || dungeon.phase === 'Running') ? myArrival(dungeon, citizen?.id ?? null, tsar) : null
  const ownHero = dungeon ? myHero(dungeon, citizen?.id ?? null, tsar) : null
  const summary = citizen ? live.citizens.get(citizen.id) : undefined
  const encounterRunId=summary?summary.encounterRunId??null:citizen?.encounterRunId??null
  useEffect(()=>{
    if(!encounterRunId||sessionStorage.getItem(sentToDungeonKey(encounterRunId)))return
    sessionStorage.setItem(sentToDungeonKey(encounterRunId),'1');navigate(dungeonPath(slug,encounterRunId))
  },[encounterRunId,slug,navigate])
  const woundedUntil = tsar ? live.state?.tsar.injuredUntil : summary ? summary.injuredUntil : citizen?.injuredUntil
  const lyingDown = !!woundedUntil && Date.parse(woundedUntil) > Date.now()
  const sendable = ownHero !== null && !ownHero.settled && !ownHero.knockedOut && !lyingDown
  useEffect(() => {
    if (!dungeon || arrival === null || !sendable) return
    if (sessionStorage.getItem(sentToDungeonKey(dungeon.id))) return
    const go = () => {
      sessionStorage.setItem(sentToDungeonKey(dungeon.id), '1')
      navigate(dungeonPath(slug, dungeon.id))
    }
    const wait = arrival - (Date.now() + serverOffset)
    if (wait <= 0) {
      go()
      return
    }
    const timer = window.setTimeout(go, wait)
    return () => window.clearTimeout(timer)
  }, [dungeon, arrival, sendable, serverOffset, slug, navigate])

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && subtitleForRef.current) {
        event.preventDefault()
        setSubtitleFor(null)
        return
      }
      if (event.key === 'Escape' && closePollRef.current) {
        event.preventDefault()
        closePollRef.current()
        return
      }
      const target = event.target as HTMLElement | null
      if (target && ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName)) {
        if (event.key === 'Escape') target.blur()
        return
      }
      if (event.key === 'Escape') {
        event.preventDefault()
        setMonasteryOpen(false)
        if (placingRef.current) {
          setPlacing(null)
          return
        }
        if (pickedRef.current) {
          setPicked(null)
          return
        }
        setPanel((current) => (current === 'none' ? 'menu' : 'none'))
      } else if (event.code === 'KeyI') {
        event.preventDefault()
        setPanel((current) => (current === 'inventory' ? 'none' : 'inventory'))
      } else if (['Equal', 'NumpadAdd'].includes(event.code)) {
        event.preventDefault()
        canvas.current?.zoom(ZOOM_STEP)
      } else if (['Minus', 'NumpadSubtract'].includes(event.code)) {
        event.preventDefault()
        canvas.current?.zoom(1 / ZOOM_STEP)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  const run = async (action: () => Promise<Citizen>) => {
    setPending(true)
    setError(null)
    try {
      applyCitizen(await action())
      setPanel('none')
      setPicked(null)
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  const start = (task: Task, spot: TilePoint | null = null, workplacePlotId?: string, farmTarget?:FarmWorkTarget) => {
    if(!farmTarget&&(['Sow','Water','Harvest'].includes(task.effect)||task.id==='tend-fields')) {
      const farms=live.state?.structures.filter(s=>s.kind==='Farm')??[]
      const farm=workplacePlotId?farms.find(s=>s.plotId===workplacePlotId):farms.find(s=>!s.ownerAccountId)??farms.find(s=>s.ownerAccountId===me.id)
      const beds=farm?live.state?.fields.filter(b=>b.plotId===farm.plotId)??[]:[]
      if(farm&&beds.length>0&&beds.every(b=>!!b.generationId)){setPanel('none');setPicked({target:{kind:'building',building:'Farm',plotId:farm.plotId,area:null,storage:false},screen:{x:innerWidth/2,y:innerHeight/3},farmTaskId:task.id});return}
    }
    const key=JSON.stringify([slug,task.id,spot,workplacePlotId,farmTarget])
    let requestId=taskStartRequests.current.get(key)
    if(!requestId){requestId=createOperationId();taskStartRequests.current.set(key,requestId)}
    return run(async()=>{const next=await api.startTask(slug,task.id,spot,workplacePlotId,{requestId,farmTarget});taskStartRequests.current.delete(key);return next})
  }
  const startJob = (choice: JobChoice) => run(() => api.startJob(slug, choice.plotId, choice.stage, choice.jobId))
  const carry = () => run(() => api.startTask(slug, CARRY_TO_STORAGE))
  const research = () => run(() => api.research(slug))

  const renew = (finished: CycleReport) => {
    if(citizen?.cycleOwnership==='PublicContract'){openMenu('village');return}
    if (finished.technologyId) {
      if (finished.technologyDiscovered) openMenu('technologies')
      else void research()
      return
    }
    if (finished.siteId) {
      const site = live.state?.sites.find((s) => s.plotId === finished.siteId)
      const stage = citizen?.siteStage ?? site?.stage ?? 0
      void startJob({ plotId: finished.siteId, stage, jobId: finished.taskId })
      return
    }
    const task = catalog?.tasks.find((t) => t.id === finished.taskId)
    if (task) {
      if(['Sow','Water','Harvest'].includes(task.effect)){void start(task,null,finished.targetPlotId??undefined);return}
      const ownership=citizen?.cycleOwnership
      if(ownership==='PrivateExtraction'||ownership==='PrivateCraft')void run(()=>api.startTask(slug,task.id,againAt(task),finished.targetPlotId??undefined,{ownership}))
      else void start(task, againAt(task), finished.targetPlotId ?? undefined)
    }
  }

  /** The same spot again, unless the picked camp tree has fallen; then the nearest tree is chopped. */
  const againAt = (task: Task) => {
    const spot = citizen?.spot
    if (!spot || !takesSpot(task)) return null
    const [x, y] = [Math.floor(spot.x), Math.floor(spot.y)]
    const state = live.state
    const felled =
      task.workplace === 'forest' &&
      inForestArea(x, y) &&
      (!state?.trees.some((tree) => tree.x === x && tree.y === y) || (state !== null && tileBuiltOver(state, x, y)))
    return felled ? null : spot
  }

  const openMenu = (next: MenuTab) => {
    setTab(next)
    setPanel('menu')
    setPicked(null)
  }

  const drop = async () => {
    setPending(true)
    setError(null)
    try {
      applyCitizen(await api.dropLoad(slug))
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  const eat = async () => {
    setPending(true)
    try { applyCitizen(await api.eat(slug)) }
    catch (exception) { setError(i18n.error(exception)) }
    finally { setPending(false) }
  }

  const drink = async () => {
    setPending(true)
    try {
      applyCitizen(await api.drink(slug))
    } catch (exception) {
      setNotice(i18n.error(exception))
      window.setTimeout(() => setNotice(null), 5000)
    } finally {
      setPending(false)
    }
  }

  const heal = async () => {
    setPending(true)
    try {
      applyCitizen(await api.heal(slug))
      setNotice(i18n.t('injury.healed'))
    } catch (exception) {
      setNotice(i18n.error(exception))
    } finally {
      setPending(false)
      window.setTimeout(() => setNotice(null), 5000)
    }
  }

  const [undeadError, setUndeadError] = useState<string | null>(null)
  const undeadAction = async (action: () => Promise<VillageState>) => {
    setPending(true)
    setUndeadError(null)
    try {
      live.setState(await action())
    } catch (exception) {
      setUndeadError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  const [dungeonError, setDungeonError] = useState<string | null>(null)
  const dungeonAction = async (action: () => Promise<VillageState>) => {
    setPending(true)
    setDungeonError(null)
    try {
      live.setState(await action())
    } catch (exception) {
      setDungeonError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  /** Keeps the storage view open, so the player sees the goods move into their hands. */
  const takeFromStorage = async (plotId: string, resource: string, amount: number, sharpen?: number, durability?: number) => {
    setPending(true)
    setError(null)
    try {
      applyCitizen(await api.takeFromStorage(slug, plotId, resource, amount, sharpen, durability))
    } catch (exception) {
      setError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  const cancelTask = async () => {
    setPending(true)
    setError(null)
    try {
      applyCitizen(await api.cancelTask(slug))
    } catch (exception) {
      setNotice(i18n.error(exception))
      window.setTimeout(() => setNotice(null), 5000)
    } finally {
      setPending(false)
    }
  }

  const { setState: setVillageState } = live
  const placement = useMemo<Placement | null>(() => {
    if (!placing) return null
    return {
      kind: placing.definition.kind,
      private: placing.private,
      onPick: async (x, y) => {
        const building = i18n.building(placing.definition.kind).name
        setPlacing(null)
        try {
          if(placing.private){
            await api.economyAction(slug,{requestId:placing.requestId!,action:'private-build',building:placing.definition.kind,x,y})
            setVillageState((await api.village(slug)).state)
          } else setVillageState(await api.placeBlueprint(slug, placing.definition.kind, x, y))
          setNotice(i18n.t(!placing.private && !tsar ? 'build.requested' : 'build.placed', { building }))
        } catch (exception) {
          setNotice(i18n.error(exception))
        }
        window.setTimeout(() => setNotice(null), 5000)
      },
    }
  }, [placing, slug, i18n, setVillageState, tsar])

  const pollAction = async (action: () => Promise<VillageState>, after?: () => void) => {
    setPending(true)
    setPollError(null)
    try {
      setVillageState(await action())
      after?.()
    } catch (exception) {
      setPollError(i18n.error(exception))
    } finally {
      setPending(false)
    }
  }

  const pollOpen = poll !== null && poll.closedAt === null
  const pollShown = pollOpen
    ? hiddenPoll !== poll.id
    : poll !== null &&
      poll.winner !== null &&
      seenPoll !== poll.id &&
      poll.closedAt !== null &&
      pollClock - Date.parse(poll.closedAt) < POLL_RESULTS_MS
  const canCallPoll = ruler && !pollOpen && (live.state?.technologies.some((state) => state.available) ?? false)

  const showPoll = () => {
    setPanel('none')
    setPollError(null)
    if (pollOpen) setHiddenPoll(null)
    else if (ruler) setPollSetup(true)
  }

  const closePoll = () => {
    setPollError(null)
    if (pollSetup) {
      setPollSetup(false)
    } else if (poll && pollOpen) {
      setHiddenPoll(poll.id)
    } else if (poll) {
      localStorage.setItem(seenPollKey(slug), poll.id)
      setSeenPoll(poll.id)
    }
  }
  useEffect(() => {
    closePollRef.current = pollSetup || pollShown ? closePoll : null
  })

  const withdraw = async (plotId: string) => {
    setVillageState(await api.cancelBlueprint(slug, plotId))
  }

  const hands = (owner: Citizen) => (
    <HandsCard citizen={owner} village={live.state!} catalog={catalog!} pending={pending} onCarry={carry} onDrop={drop} />
  )

  const villageRef = useRef(live.state)
  useEffect(() => {
    villageRef.current = live.state
  })
  const citizens = useMemo(() => [...live.citizens.values()], [live.citizens])
  const pickCitizen = useMemo(() => (id: string) => { setEnemyPick(null); if (id === PRIEST_ID) setChaplainOpen(true); else if(id===TSAR_ID&&live.state?.tsar.injuredUntil||live.citizens.get(id)?.injuredUntil||live.citizens.get(id)?.recovery?.until)setPatientFor(id); else if (tsar||citizen?.isGovernor&&citizen.canManageFarm) setSubtitleFor(id) }, [tsar,citizen?.isGovernor,citizen?.canManageFarm,live.citizens,live.state?.tsar.injuredUntil])
  // Only a citizen can be sent to work; the Tsar watches and walks around, players without a character just look.
  const canWork = !tsar && !!citizen && (!citizen.restState || citizen.restState === 'Active')
  const working = citizen?.state === 'Paused' || citizen?.state === 'Working' && !!citizen.cycleEndsAt && Date.parse(citizen.cycleEndsAt)>raidClock
  const ownId = citizen?.id
  const fighting = useMemo(() => fightingCitizenIds(live.state?.battles), [live.state?.battles])
  /** Away citizens stay on the map while a task they started is still running, or while they are still in a fight. */
  const shownCitizens = useMemo(
    () => citizens.filter((c) => c.id === ownId || fighting.has(c.id) || c.state === 'Working' || (!!c.injuredUntil&&Date.parse(c.injuredUntil)>Date.now()) || (!!c.recovery?.until&&Date.parse(c.recovery.until)>Date.now()) || !live.away.has(c.id)),
    [citizens, live.away, ownId, fighting],
  )
  const pickWorld = useMemo(() => {
    /** Water and mountains stop walkers; says so instead of asking the server. */
    const blocked = (walkerId: string, point: TilePoint) => {
      const block = canvas.current?.wayBlock(walkerId, point)
      if (!block) return false
      setNotice(i18n.t(`walk.${block}`))
      window.setTimeout(() => setNotice(null), 5000)
      return true
    }
    if (tsar) {
      return (target: WorldTarget | null, screen: { x: number; y: number }, point: TilePoint) => {
        setEnemyPick(null)
        if(point.x>=34&&point.x<=44&&point.y>=7&&point.y<=14){setMonasteryOpen(true);setPicked(null);return}
        if (target?.kind === 'building' && target.building === 'Hospital') { setHospitalOpen(true); setPicked(null); return }
        const watched = target !== null && villageRef.current !== null && watchable(target, villageRef.current)
        setPicked(watched ? { target, screen } : null)
        if (watched || blocked(TSAR_ID, point)) return
        const active=villageRef.current?.battles?.find(b=>!b.finishedAt&&!['Warning','Cancelled'].includes(b.phase))
        if(active){void api.battleOrder(slug,active.id,null,'intercept',point).catch(e=>setNotice(i18n.error(e)));return}
        api.tsarGoTo(slug, point).then(setVillageState, (exception) => {
          setNotice(i18n.error(exception))
          window.setTimeout(() => setNotice(null), 5000)
        })
      }
    }
    if (!canWork || !ownId) return (target: WorldTarget | null,screen:{x:number;y:number})=>{
      setEnemyPick(null)
      const watched=target!==null&&villageRef.current!==null&&watchable(target,villageRef.current)
      setPicked(watched?{target,screen}:null)
    }
    return (target: WorldTarget | null, screen: { x: number; y: number }, point: TilePoint) => {
      setEnemyPick(null)
      if(point.x>=34&&point.x<=44&&point.y>=7&&point.y<=14){setMonasteryOpen(true);setPicked(null);return}
      if (target?.kind === 'building' && target.building === 'Hospital') { setHospitalOpen(true); setPicked(null); return }
      setError(null)
      setPicked(target ? { target, screen } : null)
      const battle=villageRef.current?.battles?.find(b=>!b.finishedAt&&!['Warning','Cancelled'].includes(b.phase))
      if(!target&&battle){void api.battleOrder(slug,battle.id,null,'intercept',point).catch(e=>setNotice(i18n.error(e)));return}
      // Empty ground: "come here". A citizen busy with a task stays at work.
      if (target || working || blocked(ownId, point)) return
      api.goTo(slug, point).then(applyCitizen, (exception) => {
        setNotice(i18n.error(exception))
        window.setTimeout(() => setNotice(null), 5000)
      })
    }
  }, [tsar, canWork, working, ownId, slug, applyCitizen, setVillageState, i18n, me.id])
  const pickEnemy=useCallback((battleId:string,unitId:string)=>{
    if(monasteryOpen)return
    const rest = tsar ? villageRef.current?.tsar.restState : citizen?.restState
    if (rest && rest !== 'Active') { setNotice(i18n.language === 'ru' ? 'Сначала выйди из безопасного отдыха.' : 'Leave safe rest first.'); return }
    setEnemyPick({battleId,unitId,selectionToken:++enemySelection.current});setPicked(null)
    void api.battleOrder(slug,battleId,unitId,'intercept').catch(e=>{setNotice(i18n.error(e));window.setTimeout(()=>setNotice(null),5000)})
  },[monasteryOpen,slug,i18n,tsar,citizen?.restState])
  // Only the isolated E2E build exposes deterministic selection setup. Vite removes
  // this listener from production; tests still exercise the real popup/chat composition.
  useEffect(() => {
    if (import.meta.env.VITE_DESERTOD_E2E !== '1' || !pickWorld) return
    const select = (event: Event) => {
      const { target, screen, point } = (event as CustomEvent<{ target: WorldTarget; screen: { x: number; y: number }; point: TilePoint }>).detail
      pickWorld(target, screen, point)
    }
    window.addEventListener('desertovia-e2e-select', select)
    const enemy=(event:Event)=>{const {battleId,unitId}=(event as CustomEvent<{battleId:string;unitId:string}>).detail;pickEnemy(battleId,unitId)}
    window.addEventListener('desertovia-e2e-enemy',enemy)
    return () => {window.removeEventListener('desertovia-e2e-select', select);window.removeEventListener('desertovia-e2e-enemy',enemy)}
  }, [pickWorld,pickEnemy])

  const worldPickable = useMemo(
    () => (!canWork ? (target: WorldTarget) => target.kind === 'building' && target.building === 'Hospital' || villageRef.current !== null && watchable(target, villageRef.current) : null),
    [canWork],
  )
  const showPicked = picked !== null && canWork && panel === 'none'
  const showWatched = picked !== null && !canWork && panel === 'none' && live.state !== null && watchable(picked.target, live.state)
  const subtitleCitizen = subtitleFor ? live.citizens.get(subtitleFor) : undefined

  /** On phones the minimap sits right under the top bar, whose height changes with the banner and the wrapped chips. */
  const topBarObserver = useRef<ResizeObserver | null>(null)
  const watchTopBar = useCallback((hud: HTMLDivElement | null) => {
    topBarObserver.current?.disconnect()
    topBarObserver.current = null
    const top = hud?.querySelector<HTMLElement>('.hud-top')
    const root = hud?.parentElement
    if (!top || !root) return
    const measure = () => {
      // Hidden with the rest of the interface: the minimap and its buttons stay where they were.
      if (top.offsetHeight === 0) return
      root.style.setProperty('--hud-top-bottom', `${top.getBoundingClientRect().bottom - root.getBoundingClientRect().top}px`)
    }
    topBarObserver.current = new ResizeObserver(measure)
    topBarObserver.current.observe(top)
    measure()
  }, [])

  if (live.error) {
    return (
      <div className="splash">
        <p className="error">{i18n.error(live.error)}</p>
        <Link to="/">{i18n.t('toHome')}</Link>
      </div>
    )
  }

  if (!live.state || !catalog) return <div className="splash">{i18n.t('loading')}</div>
  const village = live.state
  const ownBattleFighter=(battle:NonNullable<typeof village.battles>[number])=>actorFighter(battle.fighters,me.id,ownCitizen?.id)
  const inCombat=!!village.battles?.some(b=>{const f=ownBattleFighter(b);return !b.finishedAt&&b.phase!=='Warning'&&f?.active&&f.health>0&&!f.wounded&&f.mode!=='withdrawn'})
  const shownNight = nightOverride ?? village.weather.night
  // Switching back to what the sky shows hands the light back to the clock.
  const dayNight = catalog.devTools
    ? { night: shownNight, onToggle: () => setNightOverride(!shownNight === village.weather.night ? null : !shownNight) }
    : null

  return (
    <div className={`game-root${uiHidden ? ' ui-hidden' : ''}${chatFull ? ' chat-full-active' : ''}`}>
      {atlasOpen&&<WorldAtlas routes={village.worldRoutes??[]} onFocus={point=>canvas.current?.focus(point)} onClose={()=>setAtlasOpen(false)} canTravel={(tsar||canWork&&!working)&&!village.battles?.some(b=>{const f=ownBattleFighter(b);return !b.finishedAt&&f?.active&&f.health>0})} onTravel={async point=>{if(tsar)setVillageState(await api.tsarGoTo(slug,point));else applyCitizen(await api.goTo(slug,point))}}/>}
      <GameCanvas
        ref={canvas}
        village={live.state}
        citizens={shownCitizens}
        highlightId={citizen?.id ?? null}
        nightOverride={catalog.devTools ? nightOverride : null}
        obscured={atlasOpen || monasteryOpen || walletOpen || chatFull || (panel !== 'none' && panel !== 'done') || hospitalOpen || patientFor!==null || chaplainOpen || pollSetup || subtitleFor !== null}
        catalog={catalog}
        placement={placement}
        selectedEnemyId={enemyPick?.unitId??null}
        onEnemyPick={pickEnemy}
        onCitizenPick={pickCitizen}
        onWorldPick={pickWorld}
        worldPickable={worldPickable}
        selection={showPicked || showWatched ? picked.target.area : null}
      />
      {!inCombat&&!walletOpen&&!chatFull&&<ServerClock offsetMs={serverOffset}>{now=><JourneyControl journey={tsar?village.tsar.journey:citizen?.journey??summary?.journey} now={now} onAtlas={()=>setAtlasOpen(true)} onResume={async body=>{setVillageState(await api.resumeJourney(slug,body));if(!tsar)await loadCitizen()}} onCancel={citizen?.state==='Paused'&&citizen.restState!=='RestRequested'?cancelTask:undefined}/>}</ServerClock>}

      {encounterRunId&&!walletOpen&&!chatFull&&<section className="journey-control card" aria-label={i18n.language==='ru'?'Работа и бой':'Work encounter'}>
        <strong>{i18n.language==='ru'?'Засада · задание ждёт исхода':'Ambush · your work awaits the outcome'}</strong>
        <Link className="button" to={dungeonPath(slug,encounterRunId)}>{i18n.language==='ru'?'Вернуться к бою':'Return to the fight'}</Link>
      </section>}
      {!walletOpen&&!chatFull && <button
        className="ui-toggle"
        onClick={toggleUi}
        title={i18n.t(uiHidden ? 'hud.showInterface' : 'hud.hideInterface')}
        aria-label={i18n.t(uiHidden ? 'hud.showInterface' : 'hud.hideInterface')}
        aria-pressed={uiHidden}
      >
        <EyeIcon closed={uiHidden} />
      </button>}

      <BackgroundMusic scene={villageMusicScene(village, tsar ? village.tsar : citizen, raidClock)} />

      {dayNight && (
        <button
          className="night-toggle"
          onClick={dayNight.onToggle}
          title={i18n.t('hud.dayNightTitle')}
          aria-pressed={dayNight.night}
        >
          {dayNight.night ? i18n.t('hud.day') : i18n.t('hud.night')}
        </button>
      )}

      {showWatched && (
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <WatchPopup
              target={picked.target}
              screen={picked.screen}
              phone={phone}
              village={village}
              catalog={catalog}
              citizens={citizens}
              now={now}
              actorId={me.id} tsar={tsar} ruler={ruler} onVillage={setVillageState}
              onClose={() => setPicked(null)}
            />
          )}
        </ServerClock>
      )}

      {showPicked && citizen && (
        <ServerClock key={`${picked.screen.x},${picked.screen.y},${picked.farmTaskId??''},${'plotId' in picked.target?picked.target.plotId:''}`} offsetMs={serverOffset}>
          {(now) => (
            <TargetPopup
              slug={slug}
              workers={citizens}
              initialFarmTaskId={picked.farmTaskId}
              target={picked.target}
              screen={picked.screen}
              phone={phone}
              village={village}
              catalog={catalog}
              citizen={citizen}
              now={now}
              pending={pending}
              error={error}
              onStart={start}
              onStartJob={startJob}
              onCivic={async (plotId,operation)=>setVillageState(await api.civic(slug,plotId,operation))}
              ruler={ruler}
              onTake={takeFromStorage}
              onClose={() => setPicked(null)}
            />
          )}
        </ServerClock>
      )}

      {village.defeatedAt&&<div className="defeat-banner" role="status">{i18n.language==='ru'?'Поселение разорено. Восстановите замок ремонтом. Жители и навыки сохранены.':'The settlement has fallen. Restore the castle. Residents and skills are preserved.'}</div>}
      <div className="hud" ref={watchTopBar}>
        <TopBar
          village={live.state}
          catalog={catalog}
          accountId={tsar?me.id:undefined}
          actorCitizenId={ownCitizen?.id}
          citizen={citizen}
          citizens={live.citizens}
          onForecast={async (rain) => setVillageState(await api.forecast(slug, rain))}
          wallet={(tsar||citizen)?<WalletHud slug={slug} onOpen={()=>{setWalletOpen(true);setEnemyPick(null)}}/>:null}
          rest={(tsar ? village.tsar.restState : citizen?.restState) !== undefined && (tsar || citizen) ? <SafeRestControl
            state={(tsar ? village.tsar.restState : citizen?.restState)!}
            onChange={async resting => { setVillageState(await api.setRest(slug, resting)); if (!tsar) await loadCitizen() }}
          /> : null}
        />
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <RaidBanner
              village={village}
              citizens={live.citizens}
              catalog={catalog}
              now={now}
              pending={pending || working}
            />
          )}
        </ServerClock>
        {dungeon && (
          <ServerClock offsetMs={serverOffset}>
            {(now) => (
              <DungeonBanner
                slug={slug}
                run={dungeon}
                now={now}
                canAnswer={citizenReady}
                onAnswer={() => setInviteFor(dungeon.id)}
              />
            )}
          </ServerClock>
        )}
        <div className="hud-side">
          <QuestTracker village={live.state} tsar={tsar} ruler={ruler} phone={phone} onOpen={() => openMenu('village')} />
          {(citizen !== undefined || tsar) && (
            <Guide
              key={tsar ? 'tsar' : 'citizen'}
              phone={phone}
              slug={slug}
              tsar={tsar}
              inviteCode={live.inviteCode}
              village={live.state}
              citizen={citizen}
              catalog={catalog}
              menuTab={panel === 'menu' ? tab : null}
              onMenu={openMenu}
            />
          )}
        </div>
        <ChatPanel
          actions={!walletOpen&&!chatFull&&panel==='none'&&!hospitalOpen&&!chaplainOpen&&!patientFor&&!pollSetup&&!pollShown&&(()=>{
            const current=village.battles?.filter(b=>!b.finishedAt&&b.phase!=='Warning')??[]
            const battle=current.find(b=>{const f=ownBattleFighter(b);return f?.active&&f.health>0&&!f.wounded&&f.mode!=='withdrawn'})??current.find(b=>!!ownBattleFighter(b))
            const fighter=battle?ownBattleFighter(battle):undefined
            const remedies=(village.resources.find(r=>r.kind==='Remedy')?.amount??0)+(fighter?.isTsar?0:(ownCitizen?.load.find(g=>g.resource==='Remedy')?.amount??0)+(ownCitizen?.personalGoods?.find(g=>g.kind==='Remedy')?.amount??0))
            return battle&&fighter?<ServerClock offsetMs={serverOffset}>{now=><PersonalBattlePanel slug={slug} battle={battle} fighter={fighter} remedies={remedies} now={now} onError={setNotice}/>}</ServerClock>:null
          })()}
          key={slug}
          mentionCandidates={citizens.filter(c=>!live.away.has(c.id)).map(c=>({id:c.id,name:c.name}))}
          incoming={live.chatArrivals}
          interrupted={atlasOpen || walletOpen || showPicked || showWatched || panel !== 'none' || hospitalOpen || patientFor!==null || chaplainOpen || pollSetup || pollShown || subtitleFor !== null || inviteFor !== null}
          onModeChange={mode => setChatFull(mode === 'full')}
          feed={live.feed}
          messages={live.chat}
          member={live.member}
          tsar={tsar}
          ownCitizenId={citizen?.id ?? null}
          serverOffsetMs={serverOffset}
          phone={phone}
          onSend={live.sendChat}
        />
        {!!village.worldRoutes?.length&&<button className="world-atlas-launch" aria-label={i18n.language==='ru'?'Открыть округу':'Open district atlas'} onClick={()=>{setAtlasOpen(true);setPicked(null)}}><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="m16 8-2 6-6 2 2-6Z"/><path d="M12 1v3m0 16v3M1 12h3m16 0h3"/></svg></button>}
        {(citizen || tsar) && (
          <button
            className="minimap-locate"
            onClick={() => canvas.current?.locate(citizen?.id ?? TSAR_ID)}
            title={i18n.t('hud.locate')}
            aria-label={i18n.t('hud.locate')}
          >
            <LocateIcon />
          </button>
        )}
        {!phone && (
          <div className="zoom-controls">
            <button onClick={() => canvas.current?.zoom(ZOOM_STEP)} title={`${i18n.t('hud.zoomIn')} (+)`} aria-label={i18n.t('hud.zoomIn')}>
              +
            </button>
            <button onClick={() => canvas.current?.zoom(1 / ZOOM_STEP)} title={`${i18n.t('hud.zoomOut')} (−)`} aria-label={i18n.t('hud.zoomOut')}>
              −
            </button>
          </div>
        )}
        {(placing || notice) && (
          <div className="placement-banner">
            {placing ? (
              <>
                {i18n.t(!placing.private && !tsar ? 'build.requestPlacing' : 'build.placing', { building: i18n.building(placing.definition.kind).name })}
                <button onClick={() => setPlacing(null)}>{i18n.t('build.cancel')}</button>
              </>
            ) : (
              notice
            )}
          </div>
        )}
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <BottomBar
              inCombat={inCombat}
              journeyBlocked={!!encounterRunId||!!(tsar?village.tsar.journey:citizen?.journey??summary?.journey)?.blockedAt}
              citizen={citizen}
              catalog={catalog}
              structures={village.structures}
              now={now}
              phone={phone}
              tsar={tsar}
              ruler={ruler}
              hasResult={report !== null}
              poll={
                pollOpen && !pollShown
                  ? { label: i18n.t('poll.open'), onClick: showPoll }
                  : canCallPoll && !pollSetup
                    ? { label: i18n.t('poll.call'), onClick: showPoll }
                    : null
              }
              onMenu={openMenu}
              onInventory={() => setPanel((current) => (current === 'inventory' ? 'none' : 'inventory'))}
              onShowResult={() => setPanel('done')}
              pending={pending}
              onCancelTask={cancelTask}
              drinks={
                (village.resources.find((r) => r.kind === 'VigorDrink')?.amount ?? 0) +
                (citizen?.load.find((good) => good.resource === 'VigorDrink')?.amount ?? 0) +
                (citizen?.personalGoods?.find((good) => good.kind === 'VigorDrink')?.amount ?? 0)
              }
              onDrink={drink}
              remedies={
                (village.resources.find((r) => r.kind === 'Remedy')?.amount ?? 0) +
                (citizen?.load.find((good) => good.resource === 'Remedy')?.amount ?? 0) +
                (citizen?.personalGoods?.find((good) => good.kind === 'Remedy')?.amount ?? 0)
              }
              onHeal={heal}
            />
          )}
        </ServerClock>
      </div>

      {monasteryOpen&&<MonasteryPanel slug={slug} onClose={()=>setMonasteryOpen(false)} onGo={async point=>{if(tsar)setVillageState(await api.tsarGoTo(slug,point));else setCitizen(await api.goTo(slug,point))}}/>}
      {patientFor&&(()=>{
        const royal=patientFor===TSAR_ID
        const patient=royal?live.state.tsar:live.citizens.get(patientFor)
        if(!patient)return null
        const patientId=royal?live.state.tsar.accountId:patientFor
        return <RescuePatientPanel patient={patient} now={raidClock} pending={pending}
          own={royal?tsar:citizen?.id===patientFor} canCarry={!!citizen&&citizen.id!==patientFor}
          hasHospital={live.state.structures.some(s=>s.kind==='Hospital'&&!s.ruinedAt&&s.damage<100)}
          carrierName={patient.rescueByCitizenId?live.citizens.get(patient.rescueByCitizenId)?.name:undefined}
          onClose={()=>setPatientFor(null)}
          onCarry={()=>{if(patientId){setPatientFor(null);void run(()=>api.rescue(slug,patientId))}}}
          onAsk={()=>{void (async()=>{setPending(true);try{
            const sent=await api.requestRescue(slug)
            setNotice(sent?'Просьба о помощи отправлена в игровой чат':'Просьба уже отправлена')
            setVillageState((await api.village(slug)).state)
          }catch(error){setNotice(i18n.error(error))}finally{setPending(false)}})()}}
          onTreatment={royal?undefined:()=>{setHospitalPatient(patientFor);setPatientFor(null);setHospitalOpen(true)}}/>
      })()}
      {hospitalOpen && <HospitalPanel initialPatientId={hospitalPatient} slug={slug} actorId={me.id} citizenId={citizen?.id} tsar={tsar} village={village}
        onVillage={setVillageState} serverOffsetMs={serverOffset} onClose={() => setHospitalOpen(false)} onUpdated={() => { void loadCitizen() }} />}
      {chaplainOpen && <SramoslavPanel citizen={citizen} slug={slug} onClose={() => setChaplainOpen(false)} onReply={text => canvas.current?.say(PRIEST_ID, text, 12)} />}
      {panel === 'menu' && (
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <GameMenu
              onCitizen={applyCitizen}
              slug={slug}
              tab={tab}
              onTab={setTab}
              onHospital={() => { setPanel('none'); setHospitalOpen(true) }}
              onMonastery={()=>{setPanel('none');setMonasteryOpen(true)}}
              onClose={() => setPanel('none')}
              village={village}
              population={{total:citizens.length,online:citizens.filter(c=>!live.away.has(c.id)).length}}
              tsar={tsar}
              ruler={ruler}
              inviteCode={live.inviteCode}
              onVillage={setVillageState}
              onNewInviteCode={async () => live.setInviteCode((await api.newInviteCode(slug)).inviteCode)}
              onPrivatePlace={(definition)=>{setPlacing({definition,private:true,requestId:createOperationId()});setNotice(null);setPanel('none')}}
              onPlace={(definition) => {
                setPlacing({ definition })
                setNotice(null)
                setPanel('none')
              }}
              onWithdraw={withdraw}
              catalog={catalog}
              me={me}
              citizen={citizen}
              now={now}
              pending={pending}
              error={error}
              onStart={start}
              onStartJob={startJob}
              onCancelTask={cancelTask}
              onPoll={showPoll}
              onResearch={research}
              onAccountChanged={onAccountChanged}
              onJoin={join}
              joining={joining}
              joinError={joinError}
              feed={live.feed}
              onCancelUndead={() => undeadAction(() => api.cancelUndead(slug))}
              undeadError={undeadError}
              onOpenDungeon={() => dungeonAction(() => api.openDungeon(slug))}
              onCancelDungeon={() => dungeonAction(() => api.cancelDungeon(slug))}
              dungeonError={dungeonError}
            />
          )}
        </ServerClock>
      )}

      {walletOpen&&<WalletDialog village={village} catalog={catalog} citizen={citizen} accountId={me.id} onClose={()=>setWalletOpen(false)} onPrivatePlace={definition=>{setWalletOpen(false);setPlacing({definition,private:true,requestId:createOperationId()})}}/>}

      {panel === 'inventory' && (
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <InventoryPanel
              onPrivatePlace={(definition)=>{setPlacing({definition,private:true,requestId:createOperationId()});setNotice(null);setPanel('none')}}
              village={village}
              citizen={citizen}
              catalog={catalog}
              now={now}
              pending={pending}
              error={error}
              onCarry={carry}
              onDrop={drop}
              onEat={eat}
              onCitizen={applyCitizen}
              onTake={takeFromStorage}
              onClose={() => setPanel('none')}
            />
          )}
        </ServerClock>
      )}

      {ruler && pollSetup && !pollOpen ? (
        <PollSetup
          catalog={catalog}
          village={live.state}
          pending={pending}
          error={pollError}
          onStart={(technologyIds) => pollAction(() => api.openPoll(slug, technologyIds), () => setPollSetup(false))}
          onClose={closePoll}
        />
      ) : (
        poll &&
        pollShown && (
          <PollPanel
            poll={poll}
            catalog={catalog}
            citizen={citizen}
            ruler={ruler}
            pending={pending}
            error={pollError}
            onVote={(technologyId) => pollAction(() => api.vote(slug, technologyId))}
            onCancel={() => pollAction(() => api.cancelPoll(slug))}
            onClose={closePoll}
          />
        )
      )}

      {citizen && dungeon && dungeon.phase === 'Gathering' && inviteFor === dungeon.id && (
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <DungeonInvite
              slug={slug}
              run={dungeon}
              now={now}
              citizen={citizen}
              catalog={catalog}
              onCitizen={applyCitizen}
              onClose={closeInvite}
            />
          )}
        </ServerClock>
      )}

      {(tsar||citizen?.isGovernor&&citizen.canManageFarm) && subtitleCitizen && (
        <CitizenDialog
          key={subtitleCitizen.id}
          slug={slug}
          citizen={subtitleCitizen}
          canRule={tsar}
          admissionEnabled={!!village.farmerAdmissionEnabled}
          onEnableAdmission={async()=>setVillageState(await api.enableFarmerAdmission(slug,createOperationId()))}
          onSaved={live.updateCitizen}
          onClose={() => setSubtitleFor(null)}
        />
      )}

      {panel === 'done' && report && citizen && (
        <ServerClock offsetMs={serverOffset}>
          {(now) => (
            <TaskDonePanel
              hands={hands(citizen)}
              report={report}
              citizen={citizen}
              catalog={catalog}
              now={now}
              pending={pending}
              error={error}
              onRenew={() => renew(report)}
              onChoose={() => openMenu('tasks')}
              onHide={() => setPanel('none')}
            />
          )}
        </ServerClock>
      )}
    </div>
  )
}

/** Crosshair over a dot: the view jumps to the player's own figure. */
function LocateIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" aria-hidden>
      <circle cx="12" cy="12" r="7" />
      <circle cx="12" cy="12" r="2" fill="currentColor" />
      <path d="M12 2v3M12 19v3M2 12h3M19 12h3" />
    </svg>
  )
}

/** Open eye while the interface shows, crossed out while it is hidden. */
function EyeIcon({ closed }: { closed: boolean }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z" />
      <circle cx="12" cy="12" r="3" />
      {closed && <path d="M3 3l18 18" />}
    </svg>
  )
}
