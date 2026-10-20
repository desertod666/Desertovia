import {useState} from 'react'
import {isRipe,type Catalog,type Citizen,type CitizenSummary,type FieldBed,type Task} from '../api'
import {cropTile} from '../game/worldMap'
import {useI18n} from '../i18n'
import {ActionEnergy} from './ActionEnergy'
import {Hint} from './Hint'
import {TileIcon} from './TileIcon'
import {FarmCare} from './FarmCare'

type Tool='inspect'|'sow'|'water'|'harvest'
function ToolIcon({tool}:{tool:Tool}) {
 const common={fill:'none',stroke:'currentColor',strokeWidth:2.5,strokeLinecap:'round' as const,strokeLinejoin:'round' as const}
 return <svg viewBox="0 0 40 40" width="36" height="36" aria-hidden="true" {...common}>
  {tool==='inspect'?<><circle cx="17" cy="16" r="10" fill="#e8dcb6"/><path d="m24 24 10 11" strokeWidth="5"/><path d="M13 12q2-3 6-2" stroke="#fff9de"/></>
   :tool==='sow'?<><path d="M13 7h15l-3 7q11 15 7 21H9q-4-8 8-21Z" fill="#d5ac64"/><path d="M15 14h11"/><path d="M20 29v-9m0 5q-8 0-7-6 7-1 7 6m0-1q0-7 7-7 1 7-7 7" stroke="#456c31"/></>
   :tool==='water'?<><path d="M8 16h19v18H8Z" fill="#4d9eac"/><path d="M12 16v-6q0-6 6-6t6 6v6M27 28l8-11 3 2-8 14M5 20q-9-1-4 10l7 1"/><path d="m35 9-1-4m4 9 1-4" stroke="#96e0e8"/></>
   :<><path d="M11 21V9q0-5 4-5t4 5v11-14q0-4 4-3t3 5v13-11q0-4 4-3t3 5v15q0 9-12 10-7 0-12-7l-6-7q-3-4 0-6t8 4Z" fill="#d7b478"/><path d="M17 25q6-3 11 0"/></>}
 </svg>
}

/** Select a tool, then apply it to a real bed. Cards and implicit first-bed actions are not the interaction model. */
export function FarmPlotBoard({beds,shared,now,raining,catalog,citizen,disabled=false,workers=[],initialTaskId,onWork}:{beds:FieldBed[];shared:boolean;now:number;raining:boolean;catalog:Catalog;citizen?:Citizen;disabled?:boolean;workers?:CitizenSummary[];initialTaskId?:string;onWork?:(task:Task,bed:FieldBed)=>void}) {
 const i18n=useI18n(),text=(ru:string,en:string)=>i18n.language==='ru'?ru:en
 const [tool,setTool]=useState<Tool>(()=>initialTaskId?.startsWith('sow-')?'sow':initialTaskId==='water-fields'?'water':initialTaskId==='harvest'?'harvest':'inspect'),[selected,setSelected]=useState<number|null>(null),[seedId,setSeedId]=useState(initialTaskId?.startsWith('sow-')?initialTaskId:'sow-turnips'),[notice,setNotice]=useState('')
 const labels:Record<Tool,string>={inspect:text('Осмотр','Inspect'),sow:text('Семена','Seeds'),water:text('Лейка','Water'),harvest:text('Сбор','Harvest')}
 const canAct=!!citizen&&!!onWork
 const farming=citizen?.skills.find(s=>s.skill==='Farming')?.level??0
 const seeds=catalog.tasks.filter(t=>t.effect==='Sow'&&t.skillLevel<=farming)
 const sow=seeds.find(t=>t.id===seedId)??seeds[0]
 const task=tool==='sow'?sow:catalog.tasks.find(t=>t.effect===(tool==='water'?'Water':tool==='harvest'?'Harvest':undefined))
 const chosen=beds.find(b=>b.index===selected)
 const workingOn=(bed:FieldBed)=>workers.find(w=>w.farmTargets?.some(t=>t.plotId===bed.plotId&&t.index===bed.index&&t.generationId===bed.generationId))
 const worker=chosen&&workingOn(chosen)
 const useful=(bed:FieldBed)=>tool==='inspect'||tool==='sow'&&bed.crop===null||tool==='water'&&bed.thirsty&&!raining||tool==='harvest'&&isRipe(bed)
 const status=(bed:FieldBed)=>bed.crop===null?i18n.t('field.empty'):isRipe(bed)?i18n.t('field.ripe'):
  `${i18n.crop(bed.crop)} · ${Math.floor(bed.growth*100)}% · ${raining?i18n.t('field.rain'):bed.wateredUntil&&Date.parse(bed.wateredUntil)>now?text('Влажно','Wet'):text('Сухо','Dry')}`
 const apply=(bed:FieldBed)=>{
  setSelected(bed.index);setNotice('')
  if(tool==='inspect'||disabled||!canAct)return
  const assigned=workingOn(bed)
  if(assigned){setNotice(text('Грядка занята.','This bed is busy.'));return}
  if(!useful(bed)||!task){setNotice(text('Этому участку сейчас не нужно выбранное действие.','This bed does not need the selected action right now.'));return}
  onWork?.(task,bed)
 }
 return <section className="farm-board" aria-label={text('Участок фермы','Farm plot')}>
  <div className="farm-board-heading"><span>🌱 {shared?text('Общие грядки','Shared beds'):text('Личные грядки','Private beds')}</span><Hint className="help-button" label={text('Как работать с грядками','How to work on beds')} text={canAct?text('Выберите инструмент внизу, затем нажмите на нужную грядку. Для посадки сначала выберите семена. Осмотр показывает рост и влажность.','Select a tool below, then tap the bed you want to work on. Choose seeds before planting. Inspect shows growth and moisture.'):text('Нажмите на грядку, чтобы увидеть рост, прогноз и помощь жителей.','Tap a bed to see its growth, estimates and helpers.')}>ⓘ</Hint></div>
  {raining&&<p className="farm-rain">{text('Поливает дождь','Rain waters the beds')}</p>}
  <div className="farm-ground" role="group" aria-label={text('Грядки','Beds')}>
   {beds.map(bed=><button key={bed.index} type="button" className={`farm-plot ${bed.crop===null?'empty':''} ${isRipe(bed)?'ripe':''} ${bed.thirsty?'thirsty':''} ${raining||bed.wateredUntil&&Date.parse(bed.wateredUntil)>now?'wet':'dry'} ${selected===bed.index?'selected':''} ${!useful(bed)?'not-needed':''}`} disabled={disabled&&tool!=='inspect'} aria-pressed={selected===bed.index} aria-label={i18n.t('field.bed',{number:bed.index+1})} onClick={()=>apply(bed)}>
    <span className="farm-soil" aria-hidden="true"/>
    {bed.crop&&<span className="farm-crop"><TileIcon tile={cropTile(bed.crop,bed.growth,isRipe(bed))} scale={3}/></span>}
    <span className="farm-bed-mark" aria-hidden="true">{isRipe(bed)?'✓':bed.thirsty?'💧':bed.crop?'🌱':'+'}</span>
    <span className="farm-bed-number">{bed.index+1}</span>
    {bed.crop&&<span className="farm-growth" role="progressbar" aria-label={text('Рост','Growth')} aria-valuenow={Math.floor(bed.growth*100)} aria-valuemin={0} aria-valuemax={100}><span style={{width:`${Math.min(100,Math.max(0,bed.growth*100))}%`}}/></span>}
   </button>)}
  </div>
  <div className="farm-selection" role="status">{notice|| (chosen?`${i18n.t('field.bed',{number:chosen.index+1})}: ${status(chosen)}`:canAct?text('Выберите инструмент и грядку','Choose a tool and a bed'):text('Выберите грядку','Choose a bed'))}</div>
  {worker&&<p className="farm-worker">{text('Работает','Working')}: {worker.name}</p>}
  {canAct&&<div className="farm-toolbelt" role="toolbar" aria-label={text('Инструменты фермы','Farm tools')}>
   {(['inspect','sow','water','harvest'] as const).map(item=><button type="button" key={item} aria-pressed={tool===item} aria-label={labels[item]} disabled={item!=='inspect'&&disabled||item==='water'&&raining} onClick={()=>{setTool(item);setNotice('')}}><ToolIcon tool={item}/><span>{labels[item]}</span></button>)}
  </div>}
  {canAct&&tool==='sow'&&<div className="farm-seeds" role="group" aria-label={text('Выбор семян','Choose seeds')}>{seeds.map(seed=><button key={seed.id} aria-pressed={sow?.id===seed.id} onClick={()=>setSeedId(seed.id)}>{seed.crop&&<TileIcon tile={cropTile(seed.crop,.5,false)} scale={1}/>} {seed.crop?i18n.crop(seed.crop):i18n.task(seed.id)}</button>)}</div>}
  {canAct&&tool!=='inspect'&&task&&<div className="farm-tool-note"><span>{tool==='sow'&&sow?.crop?`${labels[tool]}: ${i18n.crop(sow.crop)}`:labels[tool]} · {text('нажмите на грядку','tap a bed')}</span><ActionEnergy citizen={citizen!} action={task.id}/></div>}
  {chosen?.crop&&<details className="farm-forecast"><summary>{text('Созревание и урожай','Maturity and yield')}</summary>
   {chosen.earliestRipeAt&&chosen.latestRipeAt&&<p className="small">{text('При постоянном поливе','With continuous watering')}: {new Date(chosen.earliestRipeAt).toLocaleString(i18n.language)}<br/>{text('Без дальнейшего полива — не позднее','Without more watering — no later than')}: {new Date(chosen.latestRipeAt).toLocaleString(i18n.language)}</p>}
   <p className="small">{text('Базовый урожай до навыков и потерь','Base yield before skills and losses')}: {catalog.crops.find(c=>c.kind===chosen.crop)?.harvest.map(g=>i18n.produced(g.resource,g.amount)).join(', ')}</p>
   <p className="small muted">{text('Дождь и уход ускоряют рост. Прогноз меняется вместе с условиями.','Rain and care speed growth. Estimates change with conditions.')}</p>
  </details>}
  {chosen?.crop&&<details className="farm-care"><summary>{text('Кто помогал','Who helped')}</summary>{chosen.contributions?.length?<FarmCare actions={chosen.contributions}/>:<p className="small">{text('История ухода за этой старой посадкой неизвестна.','Care history of this older crop is unknown.')}</p>}</details>}
 </section>
}
