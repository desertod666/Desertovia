import {useRef,useState} from 'react'
import type {EconomyRequest,EconomyView,SupplyOrder} from '../api'
import {useI18n} from '../i18n'
import {ResourceIcon} from './TileIcon'
import {Hint} from './Hint'

type Action=(request:Omit<EconomyRequest,'requestId'>)=>Promise<boolean|void>
const whole=(value:string,min:number,max:number)=>/^\d+$/.test(value)&&+value>=min&&+value<=max

export function SupplyOrdersPanel({view,busy,canSupply,hasResident,onAction}:{view:EconomyView;busy:boolean;canSupply:boolean;hasResident:boolean;onAction:Action}) {
 const i18n=useI18n(),text=(r:string,e:string)=>i18n.language==='ru'?r:e
 const [resource,setResource]=useState('Wood'),[quantity,setQuantity]=useState('10'),[price,setPrice]=useState('3'),[hours,setHours]=useState('24')
 const initialReserveResource=Object.hasOwn(view.stockPrices,'Food')?'Food':Object.keys(view.stockPrices)[0]??'Wood'
 const [reserveResource,setReserveResource]=useState(initialReserveResource),[reserve,setReserve]=useState(String(view.tradeReserves?.find(r=>r.resource===initialReserveResource)?.minimumAmount??0)),[fund,setFund]=useState('10')
 const proposal=useRef<{key:string;body:Omit<EconomyRequest,'requestId'>}|null>(null)
 const valid=whole(quantity,1,10000)&&whole(price,1,1000000)&&whole(hours,1,720)
 const budget=valid?+quantity*+price:0
 const publish=async()=>{
  const key=JSON.stringify([resource,quantity,price,hours])
  if(proposal.current?.key!==key)proposal.current={key,body:{action:'publish-supply-order',resource,amount:+quantity,price:+price,expiresAt:new Date(Date.now()+ +hours*3600000).toISOString()}}
  if(await onAction(proposal.current.body))proposal.current=null
 }
 const orders=view.supplyOrders??[],active=orders.filter(o=>o.status==='Open'),closed=orders.filter(o=>o.status!=='Open')
 const resources=Object.keys(view.stockPrices)
 const chooseReserve=(value:string)=>{setReserveResource(value);setReserve(String(view.tradeReserves?.find(r=>r.resource===value)?.minimumAmount??0))}
 return <details className="supply-orders"><summary>{text('Снабжение царства','Kingdom supplies')}</summary>
  <div className="work-order-balances"><span>{text('Свободная казна','Available treasury')}: {view.treasury}</span><Hint className="help-button" label={text('Как работает поставка','How supplies work')} text={text('Вы продаёте свои материалы общине. Оплата заранее отложена; вся поставка должна поместиться на общем складе. Пока заказ открыт, общий склад не продаёт этот ресурс обратно. Общая ноша и чужие вещи не расходуются.','You sell personal materials to the community. Payment is funded in advance and the full delivery must fit in public storage. Public sales of this resource pause while the order is open. Communal loads and other people’s goods are not used.')}>ⓘ</Hint></div>
  <details><summary>{text('Пополнить фонд снабжения','Fund supplies')}</summary><div className="economy-controls">
   <label>{text('Из моего кошелька','From my wallet')}<input inputMode="numeric" value={fund} onChange={e=>setFund(e.target.value)}/></label>
   <button disabled={busy||!whole(fund,1,Math.min(view.coins,1000000))} onClick={()=>void onAction({action:'fund-treasury',price:+fund})}>{text('Передать казне','Transfer to treasury')}</button>
  </div></details>
  {view.canManageWorkOrders&&<>
   <details><summary>{text('Заказать материалы','Request supplies')}</summary><div className="economy-controls">
    <label>{text('Заказать ресурс','Requested resource')}<select value={resource} onChange={e=>setResource(e.target.value)}>{resources.map(r=><option key={r} value={r}>{i18n.resource(r)}</option>)}</select></label>
    <label>{text('Нужно единиц','Units needed')}<input inputMode="numeric" value={quantity} onChange={e=>setQuantity(e.target.value)}/></label>
    <label>{text('Цена за единицу','Price per unit')}<input inputMode="numeric" value={price} onChange={e=>setPrice(e.target.value)}/></label>
    <label>{text('Принимать часов','Accept for hours')}<input inputMode="numeric" value={hours} onChange={e=>setHours(e.target.value)}/></label>
    <p className="small">{text('Резерв оплаты','Payment fund')}: {valid?budget:'—'} {i18n.t('currency.goldUnit')}</p>
    <button disabled={busy||!valid||budget>view.treasury} onClick={()=>void publish()}>{text('Опубликовать поставку','Publish supply order')}</button>
   </div></details>
   <details><summary>{text('Сохранить общий запас','Protect public stock')}</summary><Hint className="help-button" label={text('Остаток для царства','Kingdom stock reserve')} text={text('Этот остаток исключается только из продажи общего склада. Еда для жителей, работа и уже принадлежащие людям вещи не блокируются. Ноль снимает ограничение.','This minimum is excluded from public stock sales. Meals, public work and personal goods remain available. Zero removes this limit.')}>ⓘ</Hint><div className="economy-controls">
    <label>{text('Защищаемый ресурс','Protected resource')}<select value={reserveResource} onChange={e=>chooseReserve(e.target.value)}>{resources.map(r=><option key={r} value={r}>{i18n.resource(r)}</option>)}</select></label>
    <label>{text('Оставлять не меньше','Keep at least')}<input inputMode="numeric" value={reserve} onChange={e=>setReserve(e.target.value)}/></label>
    <button disabled={busy||!whole(reserve,0,100000)} onClick={()=>void onAction({action:'set-trade-reserve',resource:reserveResource,amount:+reserve})}>{text('Сохранить остаток','Save stock reserve')}</button>
   </div></details>
  </>}
  {(view.tradeReserves??[]).length>0&&<p className="small">{text('Вне продажи','Protected from sale')}: {view.tradeReserves!.map(r=>`${i18n.resource(r.resource)} ${r.minimumAmount}`).join(' · ')}</p>}
  {active.length===0&&<p className="small muted">{text('Открытых поставок нет','No open supply orders')}</p>}
  {active.map(order=><Delivery key={order.id} order={order} owned={view.inventory.find(g=>g.kind===order.resource)?.amount??0} busy={busy} canSupply={canSupply} hasResident={hasResident} canManage={!!view.canManageWorkOrders} onAction={onAction}/>)}
  {closed.length>0&&<details><summary>{text('Недавние завершённые заказы','Recent closed orders')}</summary>{closed.map(o=><p key={o.id} className="small">{i18n.resource(o.resource)} {o.deliveredAmount}/{o.requestedAmount} · {o.status==='Completed'?text('Получено','Received'):o.status==='Expired'?text('Срок истёк','Expired'):text('Отменён','Cancelled')}</p>)}</details>}
  {(view.mySupplyDeliveries?.length??0)>0&&<details><summary>{text('Мои поставки','My deliveries')}</summary>{view.mySupplyDeliveries!.map(d=><p key={d.id} className="small">{i18n.resource(d.resource)} × {d.amount} · +{d.paidCoins} {i18n.t('currency.goldUnit')} <Hint className="help-button" label={text('Стоимость поставки','Delivery cost')} text={d.costBasisKnown?`${text('Себестоимость','Cost basis')}: ${d.costBasis}`:text('Стоимость не подтверждена; продажа не облагается до уточнения учёта.','Unknown cost; this sale is exempt until reconciliation.')}>ⓘ</Hint></p>)}</details>}
 </details>
}

function Delivery({order,owned,busy,canSupply,hasResident,canManage,onAction}:{order:SupplyOrder;owned:number;busy:boolean;canSupply:boolean;hasResident:boolean;canManage:boolean;onAction:Action}) {
 const i18n=useI18n(),text=(r:string,e:string)=>i18n.language==='ru'?r:e
 const [amount,setAmount]=useState('1'),maximum=Math.min(owned,order.remainingAmount,100)
 const allowed=canSupply&&whole(amount,1,maximum)&&Date.parse(order.expiresAt)>Date.now()
 return <section className="supply-order card" aria-label={`${text('Поставка','Supply')} ${i18n.resource(order.resource)}`}>
  <div className="supply-order-title"><ResourceIcon kind={order.resource} scale={2}/><strong>{i18n.resource(order.resource)}</strong><span>{order.deliveredAmount}/{order.requestedAmount}</span></div>
  <p className="small">{order.unitPrice} {i18n.t('currency.goldUnit')} / {text('ед.','unit')} · {text('Фонд','Fund')}: {order.reservedCoins} · <time>{new Date(order.expiresAt).toLocaleString(i18n.language)}</time></p>
  <div className="economy-controls">{hasResident&&<><label>{text('Из личных запасов','From personal goods')}<input inputMode="numeric" value={amount} onChange={e=>setAmount(e.target.value)} aria-invalid={!whole(amount,1,maximum)}/></label>
   <button disabled={busy||!allowed} onClick={()=>void onAction({action:'fulfill-supply-order',target:order.id,resource:order.resource,amount:+amount,price:order.unitPrice})}>{text('Поставить','Deliver')} · {whole(amount,1,maximum)?+amount*order.unitPrice:0} {i18n.t('currency.goldUnit')}</button>
   <p className="small muted">{text('У вас','You own')}: {owned} · {text('Осталось поставить','Still needed')}: {order.remainingAmount}</p>
   {!canSupply&&<p className="small muted">{text('Для поставки нужен житель, вернувшийся из похода и завершивший восстановление.','Deliveries require a resident back from an expedition and finished with recovery.')}</p>}
   </>}
   {canManage&&<button disabled={busy} onClick={()=>void onAction({action:'cancel-supply-order',target:order.id})}>{text('Закрыть поставку','Close supply order')}</button>}
  </div>
 </section>
}
