import {useRef,useState} from 'react'
import {api,createOperationId,type Catalog,type Citizen,type EconomyRequest,type EconomyView,type VillageState} from '../api'
import {useI18n} from '../i18n'
import {Hint} from './Hint'

/** Orders stay in a disclosure so the map and everyday phone controls remain compact. */
export function WorkOrdersPanel({village,catalog,citizen,view,busy,onAction,onCitizen,onRefresh}:{village:VillageState;catalog:Catalog;citizen?:Citizen|null;view:EconomyView;busy:boolean;onAction:(request:Omit<EconomyRequest,'requestId'>)=>Promise<boolean|void>;onCitizen?:(citizen:Citizen)=>void;onRefresh:()=>Promise<void>}) {
  const i18n=useI18n(),text=(ru:string,en:string)=>i18n.language==='ru'?ru:en
  const tasks=(catalog.tasks??[]).filter(t=>t.effect==='Resources'&&t.outputs.length>0)
  const [taskId,setTaskId]=useState('chop-wood'),[cycles,setCycles]=useState('1'),[pay,setPay]=useState('10'),[hours,setHours]=useState('24'),[fund,setFund]=useState('10')
  const [starting,setStarting]=useState(false),[error,setError]=useState('')
  const [personalTask,setPersonalTask]=useState('chop-wood')
  const requests=useRef(new Map<string,string>()),locked=useRef(false)
  const publication=useRef<{key:string;body:Omit<EconomyRequest,'requestId'>}|null>(null)
  const publish=async()=>{
    const key=JSON.stringify([taskId,cycles,pay,hours])
    if(publication.current?.key!==key)publication.current={key,body:{action:'publish-work-order',taskId,cycles:+cycles,pay:+pay,expiresAt:new Date(Date.now()+ +hours*3600000).toISOString()}}
    if(await onAction(publication.current.body))publication.current=null
  }
  const start=async(id:string,task:string)=>{
    if(locked.current)return
    let requestId=requests.current.get(id)
    if(!requestId){requestId=createOperationId();requests.current.set(id,requestId)}
    locked.current=true;setStarting(true);setError('')
    const personal=id.startsWith('personal:')
    const definition=tasks.find(t=>t.id===task)
    const workshop=personal?view.privateProperty.find(p=>!p.site&&p.kind===definition?.requiredBuilding)?.plotId:undefined
    try{const result=await api.startTask(village.slug,task,null,workshop,personal?{ownership:definition?.inputs.length?'PrivateCraft':'PrivateExtraction',requestId}:{workOrderId:id,requestId});onCitizen?.(result);requests.current.delete(id);await onRefresh()}
    catch(e){setError(i18n.error(e))}finally{locked.current=false;setStarting(false)}
  }
  const valid=/^\d+$/.test(cycles)&&+cycles>=1&&+cycles<=1000&&/^\d+$/.test(pay)&&+pay>=1&&+pay<=1000&&/^\d+$/.test(hours)&&+hours>=1&&+hours<=720
  const unavailable=busy||starting
  const cannotStart=unavailable||!citizen||citizen.restState==='Resting'||citizen.restState==='RestRequested'||citizen.state==='Working'||citizen.state==='Paused'
  return <details className="work-orders"><summary>{text('Госзаказы','Public work orders')}</summary>
    {error&&<p role="alert" className="error">{error}</p>}
    <div className="economy-balances"><span>{text('Свободная казна','Available treasury')}: {view.treasury}</span><Hint className="help-button" label={text('Условия госзаказов','Work order terms')} text={text('Один цикл — одна полезная завершённая работа. Все материалы и весь результат принадлежат общине. Оплата уже в резерве. Отмена заказа не отнимает оплату принятой работы; отмена своей работы оплаты не даёт.','One cycle is one useful completed task. All inputs and outputs belong to the community. Pay is already reserved. Closing an order preserves accepted pay; cancelling your own work earns no pay.')}>ⓘ</Hint></div>
    {view.canManageWorkOrders&&!view.workContractsEnabled&&<details><summary>{text('Перейти на оплату из казны','Switch to treasury-funded work')}</summary><p>{text('Новые общественные работы без заказа станут добровольными, без зарплаты. Уже принятая работа сохраняет прежние условия.','New public tasks without an order become voluntary, without wages. Already accepted work retains its terms.')}</p><button disabled={unavailable} onClick={()=>void onAction({action:'enable-work-contracts'})}>{text('Включить госзаказы','Enable work orders')}</button></details>}
    {view.workContractsEnabled&&<>
      <details><summary>{text('Пополнить казну','Fund the treasury')}</summary><label>{text('Сумма из моего кошелька','Amount from my wallet')}<input type="number" min="1" max="1000000" value={fund} onChange={e=>setFund(e.target.value)}/></label><button disabled={unavailable||!/^\d+$/.test(fund)||+fund<1||+fund>Math.min(view.coins,1000000)} onClick={()=>void onAction({action:'fund-treasury',price:+fund})}>{text('Передать казне','Transfer to treasury')}</button></details>
      {view.canManageWorkOrders&&<details><summary>{text('Новый заказ','New work order')}</summary><div className="economy-controls">
        <label>{text('Работа','Task')}<select value={taskId} onChange={e=>setTaskId(e.target.value)}>{tasks.map(t=><option key={t.id} value={t.id}>{i18n.task(t.id)}</option>)}</select></label>
        <label>{text('Полезных циклов','Useful cycles')}<input type="number" min="1" max="1000" value={cycles} onChange={e=>setCycles(e.target.value)}/></label>
        <label>{text('За цикл','Pay per cycle')}<input type="number" min="1" max="1000" value={pay} onChange={e=>setPay(e.target.value)}/></label>
        <label>{text('Срок приёма, часов','Acceptance window, hours')}<input type="number" min="1" max="720" value={hours} onChange={e=>setHours(e.target.value)}/></label>
        <button disabled={unavailable||!valid||+cycles*+pay>view.treasury} onClick={()=>void publish()}>{text('Зарезервировать','Reserve')} {valid?+cycles*+pay:'—'}</button>
      </div></details>}
    </>}
    {citizen&&<details><summary>{text('Личная работа','Personal work')}</summary><div className="economy-controls"><label>{text('Работа для себя','Work for myself')}<select value={personalTask} onChange={e=>setPersonalTask(e.target.value)}>{tasks.filter(t=>!t.requiredBuilding||view.privateProperty.some(p=>!p.site&&p.kind===t.requiredBuilding)).map(t=><option key={t.id} value={t.id}>{i18n.task(t.id)}</option>)}</select></label><Hint className="help-button" label={text('Условия личной работы','Personal work terms')} text={text('Расходуются только личные материалы; результат ваш. Крафт требует своей мастерской. Общий склад не расходуется, зарплаты нет.','Only personal materials are consumed; output is yours. Crafting requires your own workshop. Communal stock is not consumed; there are no wages.')}>ⓘ</Hint><button disabled={cannotStart} onClick={()=>void start('personal:'+personalTask,personalTask)}>{text('Начать для себя','Start personal work')}</button></div></details>}
    {(view.workOrders??[]).map(o=><div className="economy-lot" key={o.id}><span><strong>{i18n.task(o.taskId)}</strong> · {o.payPerCycle} {i18n.t('currency.goldUnit')}<br/>{text('Свободно','Available')}: {o.availableCycles} · {text('В работе','Accepted')}: {o.acceptedCycles}<br/>{text('В резерве','Reserved')}: {o.reservedCoins} · <time>{new Date(o.expiresAt).toLocaleString(i18n.language)}</time></span><div>
      {citizen&&o.status==='Open'&&<button disabled={cannotStart||o.availableCycles<1} onClick={()=>void start(o.id,o.taskId)}>{text('Взять цикл','Accept one cycle')}</button>}
      {view.canManageWorkOrders&&o.status==='Open'&&<button disabled={unavailable} onClick={()=>void onAction({action:'cancel-work-order',target:o.id})}>{text('Закрыть приём','Close acceptance')}</button>}
    </div></div>)}
    {(view.workPayAwaitingRoom??0)>0&&<button disabled={unavailable} onClick={()=>void onAction({action:'claim-work-pay'})}>{text('Получить заработанное','Collect earned pay')}: {view.workPayAwaitingRoom}</button>}
  </details>
}
