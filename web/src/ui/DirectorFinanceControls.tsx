import { useCallback, useEffect, useRef, useState } from 'react'
import { api, createOperationId, type DirectorFinanceLimits, type DirectorFinanceView } from '../api'

export function DirectorFinanceControls({ slug, ru }: { slug:string;ru:boolean }) {
  const [view,setView]=useState<DirectorFinanceView|null>(null)
  const [limits,setLimits]=useState<DirectorFinanceLimits|null>(null)
  const [busy,setBusy]=useState(false)
  const [error,setError]=useState('')
  const pending=useRef<{requestId:string;version:number;limits:DirectorFinanceLimits}|null>(null)
  const refresh=useCallback(async()=>{
    const next=await api.directorFinance(slug);setView(next);setLimits(next.limits)
  },[slug])
  useEffect(()=>{void refresh().catch(()=>setError(ru?'Не удалось прочитать экономические полномочия.':'Could not read economic authority.'))},[refresh,ru])
  const change=(key:keyof DirectorFinanceLimits,value:boolean|number)=>{
    pending.current=null;setLimits(old=>old?{...old,[key]:value}:old)
  }
  const save=async(stop=false)=>{
    if(!view||!limits||busy)return
    setBusy(true);setError('')
    const request=pending.current??{requestId:createOperationId(),version:view.version,limits:stop?{...view.limits,enabled:false}:limits}
    pending.current=request
    try{await api.directorFinancePolicy(slug,request.version,request.limits,request.requestId);pending.current=null;await refresh()}
    catch{setError(ru?'Не удалось подтвердить настройки. Повтори запрос или обнови данные.':'Could not confirm settings. Retry or refresh.')}
    finally{setBusy(false)}
  }
  const flag=(key:'workOrders'|'supplyOrders'|'taxChanges'|'treasuryIssues',label:string)=>
    <label><input type="checkbox" checked={limits?.[key]??false} disabled={busy} onChange={e=>change(key,e.target.checked)}/>{label}</label>
  const amount=(key:keyof DirectorFinanceLimits,label:string,min:number,max:number)=>
    <label>{label}<input type="number" min={min} max={max} step="1" value={Number(limits?.[key]??0)} disabled={busy} onChange={e=>change(key,Number(e.target.value))}/></label>
  const rate=(key:'maximumTaxBasisPoints'|'taxStepBasisPoints',label:string)=>
    <label>{label}<input type="number" min="0.01" max="100" step="0.01" value={(limits?.[key]??0)/100} disabled={busy} onChange={e=>change(key,Math.round(Number(e.target.value)*100))}/></label>
  return <details><summary>{ru?'Самостоятельная экономика':'Autonomous economy'}</summary>
    {error&&<p role="alert">{error}</p>}
    {view&&limits&&<>
      <p>{ru?'Настройки действуют в режиме «Автоэкономика». События мира по-прежнему требуют согласия.':'These limits apply in Auto economy mode. World events still require approval.'}</p>
      <p>{ru?'За сутки':'Today'}: {view.actionsToday}/{limits.actionsPerDay} · {ru?'Выделено в фонды':'Reserved for orders'}: {view.reservedToday}/{limits.spendingPerDay}</p>
      <form onSubmit={e=>{e.preventDefault();void save()}}>
        <label><input type="checkbox" checked={limits.enabled} disabled={busy} onChange={e=>change('enabled',e.target.checked)}/>{ru?'Разрешить экономические действия':'Delegate economic actions'}</label>
        {flag('workOrders',ru?'Заказы на работу':'Work orders')}
        {flag('supplyOrders',ru?'Закупки у жителей':'Purchases from residents')}
        {flag('taxChanges',ru?'Изменения будущей дани':'Future tax changes')}
        {flag('treasuryIssues',ru?'Выпуск из объявленного фонда':'Issuance from a declared fund')}
        {amount('actionsPerDay',ru?'Действий за сутки':'Actions per day',1,96)}
        {(limits.workOrders||limits.supplyOrders)&&<>
          {amount('spendingPerDay',ru?'Суточный предел новых фондов':'Daily new-order spending ceiling',1,1e12)}
          {amount('maximumUnitPay',ru?'Не больше золота за цикл/единицу товара':'Maximum coins per cycle/good',1,1000)}
          {amount('maximumUnits',ru?'Не больше циклов/товаров в заказе':'Maximum units per order',1,1000)}
        </>}
        {limits.taxChanges&&<>
          {rate('maximumTaxBasisPoints',ru?'Предел ставки, %':'Maximum rate, %')}
          {rate('taxStepBasisPoints',ru?'Шаг изменения ставки, %':'Rate change step, %')}
          {amount('taxNoticeMinutes',ru?'Уведомить до новой дани, минут':'Tax notice, minutes',1,10080)}
          {amount('taxCooldownMinutes',ru?'Перерыв между изменениями дани, минут':'Tax change cooldown, minutes',15,10080)}
        </>}
        {limits.treasuryIssues&&amount('maximumIssue',ru?'Не больше золота за один выпуск':'Maximum coins per issue',1,1e12)}
        <p className="small">{ru?'Пределы не создают деньги. Условия принятых работ и сделок сохраняются; смена настроек не возвращает потраченный суточный лимит.':'Ceilings do not create money. Accepted work and trades retain their terms; new settings do not refill the daily allowance.'}</p>
        <button type="submit" disabled={busy}>{ru?'Сохранить полномочия':'Save authority'}</button>
      </form>
      {view.limits.enabled&&<button type="button" disabled={busy} onClick={()=>{pending.current=null;void save(true)}}>{ru?'Остановить экономические действия':'Stop economic actions'}</button>}
      <details><summary>{ru?'Решения и результат':'Decisions and results'}</summary>{view.actions.map(action=><p key={action.reviewId}>
        <time>{new Date(action.at).toLocaleString()}</time> · {ru?'Всевышний':'Director'}<br/>{action.reason}<br/>{action.result}
      </p>)}</details>
      <button type="button" disabled={busy} onClick={()=>{void refresh().catch(()=>setError(ru?'Не удалось обновить.':'Could not refresh.'))}}>{ru?'Обновить экономику':'Refresh economy'}</button>
    </>}
  </details>
}
