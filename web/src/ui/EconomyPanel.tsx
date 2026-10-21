import {canUpgradeBuilding} from '../buildingLabels'
import { buildingNumber } from './buildingLabel'
import { useEffect, useRef, useState } from 'react'
import { api, ApiError, createOperationId, plotPosition, type BuildingDefinition, type Catalog, type Citizen, type EconomyRequest, type EconomyView, type VillageState } from '../api'
import { useI18n } from '../i18n'
import { EnergyDrinkCard } from './EnergyDrinkShop'
import { EconomicGoldIcon } from './EconomicGoldIcon'
import { ResourceIcon } from './TileIcon'
import { Hint } from './Hint'
import { WorkOrdersPanel } from './WorkOrdersPanel'
import { SupplyOrdersPanel } from './SupplyOrdersPanel'

/** Extends the existing City/Inventory menu. The server owns money, stock and request receipts. */
export function EconomyPanel({ village, catalog, citizen, accountId, onPrivatePlace, onCitizen, initialOpen=false }: {village: VillageState; catalog: Catalog; citizen?: Citizen|null; accountId?: string; initialOpen?:boolean; onPrivatePlace?: (building:BuildingDefinition)=>void; onCitizen?: (citizen:Citizen)=>void}) {
  const i18n = useI18n(), ru = i18n.language === 'ru'
  const text = (r: string, e: string) => ru ? r : e
  const [open, setOpen] = useState(initialOpen), [view, setView] = useState<EconomyView|null>(null)
  const [busy, setBusy] = useState(false), [error, setError] = useState(''), [notice, setNotice] = useState('')
  const [resource, setResource] = useState('Wood'), [amount, setAmount] = useState('1'), [price, setPrice] = useState('10')
  const [recipient, setRecipient] = useState(''), [filter, setFilter] = useState('')
  const [historyCursor,setHistoryCursor] = useState<string|undefined>(undefined)
  const [building, setBuilding] = useState('Hut')
  const [warehouse, setWarehouse] = useState(''), [confirmProperty,setConfirmProperty] = useState<string|null>(null)
  const requests = useRef(new Map<string, EconomyRequest>()), locked = useRef(false)
  const own = accountId ?? citizen?.accountId
  const refresh = async () => {
    if (locked.current) return
    try { setView(await api.economy(village.slug, historyCursor, filter || undefined)); setError('') }
    catch (e) { setError(i18n.error(e)) }
  }
  useEffect(() => {
    if (!open) return
    let active = true
    const update = async () => {
      if (locked.current) return
      try { const data = await api.economy(village.slug, historyCursor, filter || undefined); if (active) { setView(data); setError('') } }
      catch (e) { if (active) setError(i18n.error(e)) }
    }
    void update(); const timer = window.setInterval(() => void update(), 15000)
    return () => { active = false; window.clearInterval(timer) }
  }, [open, village.slug, filter, historyCursor, i18n])
  const act = async (body: Omit<EconomyRequest, 'requestId'>) => {
    if (locked.current) return
    const key = JSON.stringify(body)
    let request = requests.current.get(key)
    if (!request) {
      request = {...body, requestId: createOperationId()}
      requests.current.set(key, request)
    }
    locked.current = true; setBusy(true); setError(''); setNotice('')
    try {
      // New servers settle against a durable quote. Keep it and the trade ID after an uncertain network result.
      if(request.action==='buy-lot' && request.target && !request.taxQuoteId && view?.latestTaxPolicyVersion!==undefined){
        const quote=await api.marketTaxQuote(village.slug,request.requestId,request.target)
        const offer=view.lots.find(l=>l.id===request.target)
        if(!offer || quote.buyerPays!==offer.price)throw new ApiError(400,text('Цена покупки изменилась. Обнови рынок.','The purchase price changed. Refresh the market.'),'economy.priceChanged')
        request.taxQuoteId=quote.id
      }
      setView(await api.economyAction(village.slug, request)); setHistoryCursor(undefined); requests.current.delete(key); setNotice(text('Готово', 'Done')); return true
    }
    catch (e) {
      // These explicit server rejections prove no trade committed; next click may request fresh terms.
      if(e instanceof ApiError && e.status===400 && ['tax.quote.stale','tax.quote.missing','tax.quote.required','economy.priceChanged'].includes(e.code??''))requests.current.delete(key)
      setError(i18n.error(e)); return false
    }
    finally { locked.current = false; setBusy(false) }
  }
  const units = Number(amount), validAmount = /^\d+$/.test(amount) && units >= 1 && units <= 100
  const validPrice = /^\d+$/.test(price) && Number(price) >= 1 && Number(price) <= 1000000
  const unitPrice = (kind: string) => view?.stockPrices[kind] ?? 0
  const choices = village.resources.filter(r => r.kind !== 'ResearchPoints')
  const supplyVerbs:Record<string,string>={'supply-reserve':text('Фонд поставки','Supply fund'),'supply-payment':text('Оплата поставки','Supply payment'),'supply-refund':text('Возврат фонда','Supply fund returned')}
  const location = (value: string) => {
    const [scope,...parts] = value.split(':'); const plot=parts.join(':')
    if(scope==='village')return text('общий запас','communal stock')
    if(scope==='personal')return text('личное имущество','personal goods')
    if(scope==='recovery'||scope==='private-recovery')return text('материалы после разборки','recovered construction materials')
    if(scope==='hands')return text('ноша','load')
    if(scope==='market')return text('резерв рынка','market reserve')
    if(scope==='supply-order')return text('фонд поставки','supply fund')
    const target=[...(village.structures??[]),...(village.sites??[])].find(b=>b.plotId===plot)
    const storage=(village.storages??[]).find(b=>b.plotId===plot)
    return target?`${i18n.building(target.kind).name} · ${buildingNumber(village,plot)}`:storage?`${i18n.storage(storage.kind)} · ${buildingNumber(village,plot)}`:value==='wallet'?text('кошелёк','wallet'):value==='treasury'?text('казна','treasury'):value
  }
  const verb = (s: string) => ({opening: text('Начальный остаток','Opening balance'), wage: text('Оплата работы','Work pay'), 'work-completed': text('Полезная работа','Useful work'), 'buy-stock': text('Покупка у царства','Kingdom purchase'), 'donate-stock': text('Дар царству','Donation to kingdom'), trade: text('Торговля','Trade'), 'energy-drink': text('Энергетик батюшки','Chaplain energy drink'), 'exchange-burn': i18n.t('economy.exchangeGold'), exchange: text('Тестовые свечи','Test candles'), transfer: text('Передача','Transfer'), list: text('Резерв лота','Lot reserved')})[s] ?? text('Изменение запаса','Stock change')
  return <details className="card economy-panel" open={open} onToggle={e => setOpen(e.currentTarget.open)}>
    <summary>{text('Кошелёк, имущество и рынок','Wallet, possessions and market')}</summary>
    {open && <>
      {error && <p role="alert" className="error">{error}</p>}{notice && <p role="status">{notice}</p>}
      {!view ? <p>{i18n.t('loading')}</p> : <>
        <div className="economy-balances"><strong><EconomicGoldIcon/> {view.coins} {i18n.t('currency.goldUnit')}</strong><span>🕯 {view.testCandles} {text('тестовых свечей','test candles')}</span><Hint className="help-button" label={text('О деньгах и оплате труда','About money and work pay')} text={[i18n.t('economy.goldDescription'), `${text('Полезная работа','Useful work')}: ${view.workPay} · ${text('Стройка и ремонт','Construction and repair')}: ${view.constructionPay} ${i18n.t('economy.privateConstructionPay')}`].join('\n')}>ⓘ</Hint></div>
        {citizen && <details className="energy-shop-disclosure"><summary>{text('Энергетик батюшки','Chaplain energy drink')}</summary><EnergyDrinkCard view={view} citizen={citizen} busy={busy} showBalance={false} onBuy={currency=>void act({action:'energy-drink-'+currency})}/></details>}
        <details><summary>{text('Моё имущество и передача','My possessions and transfers')}</summary>
          <p className="small">{text('Личное имущество хранится отдельно от общей ноши и склада. До 100 каждого материала.','Personal goods are separate from communal loads and storage. Up to 100 of each material.')}</p>
          {citizen && <p>{text('Общая экипировка на время работы','Communal gear borrowed for work')}: {citizen.weapon ? <><ResourceIcon kind={citizen.weapon} scale={1} sharpen={citizen.weaponSharpen ?? 0} /> {i18n.resource(citizen.weapon)}</> : '—'} · {citizen.armor ? i18n.resource(citizen.armor) : '—'}. {text('Вернётся на склад после работы.','Returned after work.')}</p>}
          <div className="inventory-grid">{view.inventory.map(g => <div className="slot" key={g.kind}><ResourceIcon kind={g.kind} scale={2}/><span>{i18n.resource(g.kind)}</span><strong>{g.amount}/100</strong><Hint className="help-button inventory-cost" label={text('Себестоимость','Acquisition cost')} text={g.costBasisKnown
            ? `${text('Себестоимость всей партии','Whole stack acquisition cost')}: ${g.costBasis} ${i18n.t('currency.goldUnit')}`
            : text('История стоимости этой партии не подтверждена. Продажа не включается в налогооблагаемую прибыль до уточнения учёта. Это не означает нулевую стоимость.','This stack’s cost history is unverified. Its sale is excluded from taxable profit until reconciliation. This does not mean its cost was zero.')}>ⓘ</Hint></div>)}</div>
          {view.inventory.length === 0 && <p className="muted small">{text('Личных вещей пока нет. Их можно купить у царства или добыть за его границей.','No personal goods yet. Buy from the kingdom or gather outside its boundary.')}</p>}
        </details>
        {citizen && <section className="economy-controls">
          <label>{text('Материал','Material')}<select value={resource} onChange={e=>setResource(e.target.value)}>{choices.map(r=><option key={r.kind} value={r.kind}>{i18n.resource(r.kind)}</option>)}</select></label>
          <label>{text('Количество','Amount')}<input inputMode="numeric" type="text" value={amount} onChange={e=>setAmount(e.target.value)} aria-invalid={!validAmount}/></label>
          <button disabled={busy||!validAmount||(view.stockForSale?.[resource]??Infinity)<units} onClick={()=>void act({action:'buy-stock',resource,amount:units,price:unitPrice(resource)})}>{text('Купить у царства','Buy from kingdom')} · {validAmount ? unitPrice(resource)*units : '—'} {i18n.t('currency.goldUnit')}</button>
          <button disabled={busy||!validAmount||(view.inventory.find(g=>g.kind===resource)?.amount??0)<units} onClick={()=>void act({action:'donate-stock',resource,amount:units})}>{text('В общий запас · бесплатно','Donate · free')}</button>
          <p className="small muted">{text('Что не поместится, останется у тебя.','Anything that does not fit stays with you.')}</p>
          {view.stockForSale&&<p className="small muted">{text('В продаже','For sale')}: {view.stockForSale[resource]??0}{view.supplyOrders?.some(o=>o.status==='Open'&&o.resource===resource)?text(' · царство закупает этот ресурс',' · the kingdom is buying this resource'):''}</p>}
        </section>}
        {citizen && <details className="economy-transfer"><summary>{text('Передать или выставить на рынок','Transfer or list on market')}</summary>
          <p className="small">{i18n.resource(resource)} × {validAmount ? units : '—'} · {text('Материал и количество выбери выше.','Choose the material and amount above.')}</p>
          <div className="economy-controls"><label>{text('Получатель','Recipient')}<select value={recipient} onChange={e=>setRecipient(e.target.value)}><option value="">{text('Выбрать жителя','Choose a resident')}</option>{view.residents.filter(r=>r.accountId!==own).map(r=><option key={r.accountId} value={r.accountId}>{r.name}</option>)}</select></label>
          <button disabled={busy||!validAmount||!recipient} onClick={()=>void act({action:'transfer',resource,amount:units,target:recipient})}>{text('Передать личное','Give personal goods')}</button>
          <label>{i18n.t('economy.lotPrice')}<input inputMode="numeric" value={price} onChange={e=>setPrice(e.target.value)} aria-invalid={!validPrice}/></label>
          <button disabled={busy||!validAmount||!validPrice} onClick={()=>void act({action:'list',resource,amount:units,price:Number(price)})}>{text('Выставить личное на рынок','List personal goods')}</button>
        </div></details>}
        <details><summary>{text('Рынок','Market')}</summary>
          {view.lots.length===0 && <p className="muted">{text('Лотов пока нет','No lots yet')}</p>}
          {view.lots.map(l=><article className="economy-lot" key={l.id}><ResourceIcon kind={l.resource} scale={2}/><div><strong>{i18n.resource(l.resource)} × {l.amount}</strong><span>{l.price} {i18n.t('currency.goldUnit')} · {l.seller}</span></div>
            {l.status==='open' ? <button disabled={busy||(!citizen&&l.sellerAccountId!==own)} onClick={()=>void act({action:l.sellerAccountId===own?'cancel-lot':'buy-lot',target:l.id})}>{l.sellerAccountId===own?text('Снять','Cancel'):text('Купить','Buy')}</button> : <span>{l.status==='sold'?text('Продано','Sold'):text('Снят','Cancelled')}</span>}</article>)}
        </details>
        <details><summary>{text('Обмен и налог','Exchange and tax')}</summary>
          <p>{view.exchangeRate} {i18n.t('economy.goldToTestCandle')}. {text('Осталось лично','Personal remaining')}: {Math.max(0,view.dailyExchangeLimit-view.exchangedToday)} · {text('Фонд царства сегодня','Kingdom remaining today')}: {view.kingdomCandleRoom}.</p>
          <button disabled={busy||view.coins<view.exchangeRate||view.exchangedToday>=view.dailyExchangeLimit||view.kingdomCandleRoom<1} onClick={()=>void act({action:'exchange',amount:1})}>{text('Обменять на 1 тестовую свечу','Exchange for 1 test candle')}</button>
          <p className="small">{text('Сброс лимита','Limit resets')}: {new Date(view.dayResetsAt).toLocaleString(i18n.language)}. {text('Свечи аккаунта батюшки не начисляются.','No chaplain account candles are credited.')}</p>
          <p>{text('Заработок за сутки','Work income today')}: {view.earnedToday} · {text('Торговая прибыль','Trading profit')}: {view.profitToday}.</p>
          <p>{text('Удержано дани сегодня','Tax paid today')}: {view.taxPaidToday ?? 0} {i18n.t('currency.goldUnit')}. {text('Условия определяет опубликованная политика; зарплата и неподтверждённая себестоимость освобождены. Условия ранее выставленных лотов сохраняются.','Published policy determines settlement; wages and unverified costs are exempt. Existing offers retain their terms.')}</p>
        </details>
        {citizen && <details><summary>{text('Личная стройка за царством','Private construction outside the kingdom')}</summary>
          <p className="small">{text('Только твои материалы, без общего склада и казённой зарплаты. Участок должен быть разведан и доступен.','Your own materials only, with no communal supplies or wages. The plot must be explored and reachable.')}</p>
          <div className="economy-controls"><label>{text('Здание','Building')}<select value={building} onChange={e=>setBuilding(e.target.value)}>{catalog.buildings.filter(b=>b.stages.length>0&&!['Castle','Hospital','RopeBridge'].includes(b.kind)).map(b=><option key={b.kind} value={b.kind}>{i18n.building(b.kind).name}</option>)}</select></label>
            <button disabled={busy||!onPrivatePlace} onClick={()=>{const definition=catalog.buildings.find(b=>b.kind===building);if(definition)onPrivatePlace?.(definition)}}>{text('Выбрать место на карте','Choose a plot on the map')}</button>
          </div>
          {view.privateProperty.map(p=><div className="economy-lot" key={(p.site?'site:':'building:')+p.plotId}><span>{i18n.building(p.kind).name} · {buildingNumber(village,p.plotId)} · {p.site?text('Строится','Under construction'):text('Уровень','Level')+' '+p.level}</span><div>
            {!p.site&&<button disabled={busy||!canUpgradeBuilding(village,p.plotId)} onClick={()=>{const at=plotPosition(p.plotId);if(at)void act({action:'private-upgrade',x:at.x,y:at.y})}}>{text('Улучшить','Upgrade')}</button>}
            {p.site&&!p.hasWork&&<button disabled={busy} onClick={()=>{const at=plotPosition(p.plotId);if(at)void act({action:'private-cancel',x:at.x,y:at.y})}}>{text('Отменить разметку','Cancel blueprint')}</button>}
            {!p.site&&(confirmProperty===p.plotId?<><span>{text('Начать медленную разборку?','Start slow dismantling?')}</span><button disabled={busy} onClick={()=>{const at=plotPosition(p.plotId);if(at)void act({action:'private-demolish',x:at.x,y:at.y});setConfirmProperty(null)}}>{text('Подтвердить','Confirm')}</button><button onClick={()=>setConfirmProperty(null)}>{text('Отмена','Cancel')}</button></>:<button onClick={()=>setConfirmProperty(p.plotId)}>{text('Разобрать','Dismantle')}</button>)}
          </div></div>)}
          {view.privateStorages.length>0&&<div className="economy-controls"><label>{text('Мой склад','My warehouse')}<select value={warehouse} onChange={e=>setWarehouse(e.target.value)}><option value="">—</option>{view.privateStorages.map(s=><option key={s.plotId} value={s.plotId}>{i18n.storage(s.kind)} · {buildingNumber(village,s.plotId)}</option>)}</select></label>
            <button disabled={busy||!warehouse||!validAmount} onClick={()=>{const p=plotPosition(warehouse);if(p)void act({action:'deposit-private',resource,amount:units,x:p.x,y:p.y})}}>{text('Положить личное','Deposit personal goods')}</button>
            {view.privateStorages.filter(s=>s.plotId===warehouse).flatMap(s=>s.goods.map(g=><button key={`${g.resource}:${g.sharpen ?? 0}:${g.durability ?? 0}`} disabled={busy} onClick={async()=>{if(locked.current)return;locked.current=true;setBusy(true);try{await api.takeFromStorage(village.slug,s.plotId,g.resource,1,g.sharpen ?? 0,g.maxDurability?g.durability:undefined)}catch(e){setError(i18n.error(e))}finally{locked.current=false;setBusy(false);void refresh()}}}>{text('Взять','Take')} <ResourceIcon kind={g.resource} scale={1} sharpen={g.sharpen ?? 0} durability={g.durability} maxDurability={g.maxDurability} /> {i18n.resource(g.resource)} · {g.amount}</button>))}
          </div>}
        </details>}
        {(view.siteRecoveries?.length??0)>0&&<section className="card"><h3>{text('Материалы после разборки','Recovered materials')}</h3>{view.siteRecoveries!.map(r=><div key={r.id} className="economy-lot"><span>{buildingNumber(village,r.plotId)} · {r.personal?text('Личные','Personal'):text('Общие','Communal')}<br/>{r.goods.map(g=>`${i18n.resource(g.resource)} × ${g.amount}`).join(', ')}</span><button disabled={busy} onClick={()=>void act({action:'claim-site-recovery',target:r.id})}>{text('Забрать','Collect')}</button></div>)}<p className="small muted">{text('Если места не хватает, материалы останутся здесь.','Materials stay here if there is not enough room.')}</p></section>}
        <details><summary>{text('Журнал царства','Kingdom journal')}</summary>
          <p className="small muted">{text('Учёт с','Recorded since')} {new Date(view.startedAt).toLocaleString(i18n.language)} · {text('Казна','Treasury')}: {view.treasury} {i18n.t('currency.goldUnit')}.</p>
          <label>{text('Фильтр','Filter')}<select value={filter} onChange={e=>{setHistoryCursor(undefined);setFilter(e.target.value)}}><option value="">{text('Все операции','All entries')}</option><option value={view.currencies?.coins.ledgerAsset??'DZK'}>{i18n.t('currency.economicGold')}</option>{choices.map(r=><option value={r.kind} key={r.kind}>{i18n.resource(r.kind)}</option>)}</select></label>
          <ol className="economy-journal">{view.journal.map(row=><li key={row.id}><time>{new Date(row.at).toLocaleTimeString(i18n.language,{hour:'2-digit',minute:'2-digit'})}</time> <strong>{row.delta>0?'+':''}{row.delta}</strong> {row.asset===(view.currencies?.coins.ledgerAsset??'DZK')?i18n.t('currency.goldUnit'):row.asset==='TEST_CANDLE'?text('тестовых свечей','test candles'):row.asset.startsWith('work:')?text('вклад','contribution'):i18n.resource(row.asset)}<br/><small>{supplyVerbs[row.operation]??verb(row.operation)} · {row.actor??text('Царство','Kingdom')} · {location(row.location)}</small></li>)}</ol>
          {view.nextJournalBefore&&<button disabled={busy} onClick={()=>setHistoryCursor(view.nextJournalBefore!)}>{text('Раньше','Older entries')}</button>}
          {historyCursor&&<button onClick={()=>setHistoryCursor(undefined)}>{text('К новым записям','Latest entries')}</button>}
        </details>
        <button disabled={busy} onClick={()=>void refresh()}>{text('Обновить','Refresh')}</button>
        <WorkOrdersPanel village={village} catalog={catalog} citizen={citizen} view={view} busy={busy} onAction={act} onCitizen={onCitizen} onRefresh={refresh}/>
        <SupplyOrdersPanel view={view} busy={busy} hasResident={!!citizen} canSupply={!!citizen&&citizen.status!=='Dormant'&&citizen.state!=='Paused'
          &&!(Date.parse(citizen.injuredUntil??'')>Date.now())&&!(Date.parse(citizen.exhaustedUntil??'')>Date.now())
          &&!village.dungeon?.heroes.some(h=>h.citizenId===citizen.id&&!h.settled)} onAction={act}/>
      </>}
    </>}
  </details>
}
