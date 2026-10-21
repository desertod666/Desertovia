import { canBuildAt } from '../src/game/worldMap'
import { createRoot } from 'react-dom/client'
import { api, ApiError, type Catalog, type Citizen, type EconomyRequest, type EconomyView, type VillageState } from '../src/api'
import { I18nProvider } from '../src/i18n'
import { EconomyPanel } from '../src/ui/EconomyPanel'
import { ActionEnergy } from '../src/ui/ActionEnergy'
import { StorageCard } from '../src/ui/StorageCard'
import { HandsCard } from '../src/ui/HandsCard'
import { useState } from 'react'
import '../src/index.css'

declare global { interface Window { economyCalls: EconomyRequest[]; workCalls:unknown[]; failEconomyOnce: boolean; kingdomCanBuild: (personal:boolean,x:number,y:number)=>boolean;taxQuoteCalls:{requestId:string;lotId:string}[];failAfterMarketCommitOnce:boolean;staleMarketQuoteOnce:boolean } }
window.workCalls=[]
window.economyCalls=[];window.failEconomyOnce=false
const view: EconomyView={energyDrinkReadyAt:null,coins:1000,testCandles:0,earnedToday:100,profitToday:0,taxPreview:0,taxEnabled:false,exchangeRate:100,dailyExchangeLimit:5,exchangedToday:0,kingdomCandleRoom:50,dayResetsAt:new Date(Date.now()+3600000).toISOString(),treasury:0,inventory:[],lots:[],journal:[],nextJournalBefore:null,startedAt:new Date().toISOString(),workPay:10,constructionPay:20,residents:[{accountId:'me',name:'Игрок'},{accountId:'other',name:'Очень длинное имя другого игрока'}],privateStorages:[],stockPrices:{Wood:2,Stone:3,Planks:8},privateProperty:[]}
const taxFixture=new URLSearchParams(location.search).has('tax')
const settledMarketRequests=new Set<string>()
window.taxQuoteCalls=[];window.failAfterMarketCommitOnce=false;window.staleMarketQuoteOnce=false
if(taxFixture){
  view.latestTaxPolicyVersion=1;view.taxPaidToday=0
  view.lots=[{id:'33333333-3333-4333-8333-333333333333',sellerAccountId:'other',seller:'Продавец',resource:'Wood',amount:1,price:100,status:'open'}]
  api.marketTaxQuote=async(_slug,requestId,lotId)=>{window.taxQuoteCalls.push({requestId,lotId});return{id:requestId,lotId,buyerPays:100,policyVersion:1,expiresAt:new Date(Date.now()+120000).toISOString()}}
}
const costFixture = new URLSearchParams(location.search).get('cost')
const orderFixture = new URLSearchParams(location.search).get('orders')
if(orderFixture){view.workContractsEnabled=orderFixture!=='legacy';view.canManageWorkOrders=orderFixture!=='worker';view.treasury=100;view.workOrders=orderFixture==='worker'?[{id:'11111111-1111-4111-8111-111111111111',taskId:'chop-wood',policyVersion:1,payPerCycle:10,availableCycles:1,acceptedCycles:0,completedCycles:0,reservedCoins:10,expiresAt:new Date(Date.now()+3600000).toISOString(),status:'Open'}]:[]}
if (costFixture) view.inventory = [{kind:'Wood', amount:4, costBasis:9, costBasisKnown:costFixture==='known'}]
const supplyFixture=new URLSearchParams(location.search).get('supply')
if(supplyFixture){view.canManageWorkOrders=supplyFixture==='owner';view.treasury=100;view.inventory=[{kind:'Wood',amount:4,costBasis:9,costBasisKnown:true}];view.tradeReserves=[];view.stockForSale={Wood:supplyFixture==='worker'?0:10,Stone:8,Planks:2};view.supplyOrders=supplyFixture==='worker'?[{id:'22222222-2222-4222-8222-222222222222',resource:'Wood',requestedAmount:4,remainingAmount:4,deliveredAmount:0,unitPrice:3,reservedCoins:12,expiresAt:new Date(Date.now()+3600000).toISOString(),status:'Open'}]:[]}
api.economy=async()=>structuredClone(view)
api.economyAction=async(_slug,request)=>{
  window.economyCalls.push(structuredClone(request))
  if(taxFixture&&request.action==='buy-lot'){
    if(window.staleMarketQuoteOnce){window.staleMarketQuoteOnce=false;throw new ApiError(400,'Расчёт устарел. Повтори покупку.','tax.quote.stale')}
    if(!settledMarketRequests.has(request.requestId)){view.coins-=100;view.lots[0]!.status='sold';settledMarketRequests.add(request.requestId)}
    if(window.failAfterMarketCommitOnce){window.failAfterMarketCommitOnce=false;throw new Error('Обрыв связи после покупки. Повтори запрос.')}
    return structuredClone(view)
  }
  if(window.failEconomyOnce){window.failEconomyOnce=false;throw new Error('Обрыв связи. Повтори запрос.')}
  if(request.action==='buy-stock'){const amount=request.amount??1;view.coins-=amount*2;view.treasury+=amount*2;view.inventory=[{kind:'Wood',amount,costBasis:amount*2}]}
  if(request.action==='publish-work-order'){const cycles=request.cycles!,pay=request.pay!;view.treasury-=cycles*pay;view.workOrders=[...(view.workOrders??[]),{id:request.requestId,taskId:request.taskId!,policyVersion:1,payPerCycle:pay,availableCycles:cycles,acceptedCycles:0,completedCycles:0,reservedCoins:cycles*pay,expiresAt:request.expiresAt!,status:'Open'}]}
  if(request.action==='enable-work-contracts')view.workContractsEnabled=true
  if(request.action==='publish-supply-order'){const amount=request.amount!,price=request.price!;view.treasury-=amount*price;view.stockForSale![request.resource!]=0;view.supplyOrders=[...(view.supplyOrders??[]),{id:request.requestId,resource:request.resource!,requestedAmount:amount,remainingAmount:amount,deliveredAmount:0,unitPrice:price,reservedCoins:amount*price,expiresAt:request.expiresAt!,status:'Open'}]}
  if(request.action==='set-trade-reserve'){view.tradeReserves=[...(view.tradeReserves??[]).filter(r=>r.resource!==request.resource),...request.amount?[{resource:request.resource!,minimumAmount:request.amount}]:[]]}
  if(request.action==='fulfill-supply-order'){const order=view.supplyOrders!.find(o=>o.id===request.target)!,amount=request.amount!;order.remainingAmount-=amount;order.deliveredAmount+=amount;order.reservedCoins-=amount*order.unitPrice;view.coins+=amount*order.unitPrice;const good=view.inventory.find(g=>g.kind===order.resource)!;good.amount-=amount;if(order.remainingAmount===0)order.status='Completed'}
  return structuredClone(view)
}
const village={id:'00000000-0000-0000-0000-000000000001',slug:'test',structures:[],storages:[],sites:[],bridgeOpened:false,completedProjects:[],kingdomArea:{x:-4,y:-3,width:32,height:32},resources:[{kind:'Wood',amount:10,capacity:30},{kind:'Stone',amount:8,capacity:30},{kind:'Planks',amount:2,capacity:30}]} as unknown as VillageState
const catalog={tasks:[{id:'chop-wood',effect:'Resources',inputs:[],outputs:[{resource:'Wood',amount:5}],requiredBuilding:null}],buildings:[],buildArea:{x:0,y:0,width:28,height:26},reservedAreas:[]} as unknown as Catalog
window.kingdomCanBuild=(personal,x,y)=>canBuildAt(catalog,village,x,y,personal)
const citizen={energy:0,exhaustedCycles:2,actionEnergyCosts:{'test-work':2},accountId:'me',name:'Игрок',weapon:null,armor:null} as unknown as Citizen
api.startTask=async(_slug,taskId,_spot,_workplace,terms)=>{
  window.workCalls.push({taskId,...terms})
  await new Promise(resolve=>setTimeout(resolve,100))
  if(terms?.workOrderId){view.workOrders![0]!.availableCycles=0;view.workOrders![0]!.acceptedCycles=1}
  return {...citizen,state:'Working',restState:'Active',taskId,cycleOwnership:terms?.workOrderId?'PublicContract':terms?.ownership,cyclePaymentMode:terms?.workOrderId?'Reserved':'Voluntary'} as Citizen
}
function EconomyStand(){const [current,setCurrent]=useState<Citizen|null>(new URLSearchParams(location.search).has('tsar')?null:citizen);return <EconomyPanel village={village} catalog={catalog} citizen={current} accountId="me" onCitizen={setCurrent}/>}
function ToolStand() {
  const [taken,setTaken]=useState('')
  const held={...citizen,load:[{resource:'StoneAxe',amount:1,durability:3,maxDurability:8}]}
  const empty={...citizen,load:[]}
  const toolCatalog={...catalog,storages:[{kind:'TownHall',slots:8,slotSize:15,accepts:['StoneAxe','Sword']}],rules:{takeLimit:10}} as unknown as Catalog
  const storage={kind:'TownHall',plotId:'3,3',damage:0,slots:[],goods:[{resource:'StoneAxe',amount:1,durability:3,maxDurability:8},{resource:'StoneAxe',amount:1,durability:8,maxDurability:8},{resource:'Sword',amount:1,sharpen:2}]}
  return <><HandsCard citizen={held} catalog={toolCatalog} village={village} pending={false} onCarry={()=>{}} onDrop={()=>{}}/><StorageCard storage={storage} catalog={toolCatalog} take={{citizen:empty,disabled:false,onTake:(...values)=>setTaken(JSON.stringify(values))}}/><output id="tool-take">{taken}</output></>
}
createRoot(document.getElementById('app')!).render(<I18nProvider><div className="modal-backdrop"><section className="modal menu"><div className="modal-body">{new URLSearchParams(location.search).has('tools')?<ToolStand/>:<><div id="faint-warning"><ActionEnergy citizen={citizen} action="test-work"/></div><EconomyStand/></>}</div></section></div></I18nProvider>)
