import { createRoot } from 'react-dom/client'
import { api, type AlmightyView, type DirectorFinanceView } from '../src/api'
import { I18nProvider } from '../src/i18n'
import { AlmightyPanel } from '../src/ui/AlmightyPanel'
import '../src/index.css'
const view: AlmightyView = { enabled:true, state:{mode:'Propose',policyVersion:0,disabledKinds:[],intent:'Сначала восстановить охрану и оценить запасы, затем предложить событие.',lastReviewAt:new Date().toISOString(),nextReviewAt:new Date(Date.now()+900000).toISOString(),restUntil:null,lastError:'',callsToday:1,lastSnapshot:'{}'},
 proposals:[{id:'one',option:'economy_report',reason:'Я вижу что городу нужны доски и ремонт. Предлагаю согласовать работы, сохранив медленный темп игры.',intent:'Наблюдать',status:'Pending',createdAt:new Date().toISOString(),expiresAt:new Date(Date.now()+1800000).toISOString(),noticeState:'Sent',result:''}], observations:[] }
api.almighty=async()=>structuredClone(view)
api.almightyMode=async(_slug,mode)=>{view.state.mode=mode}
const finance:DirectorFinanceView={version:1,day:'2026-10-21',actionsToday:1,reservedToday:40,
 actions:[{reviewId:'live',author:'director',option:'finance_work_low',reason:'Я вижу нужду в запасах.',result:'Выделено40 в фонд.',at:new Date().toISOString(),applied:true},
 {reviewId:'shadow',author:'director',option:'finance_issue',reason:'Сравниваю нехватку фонда.',result:'Ничего не исполнено.',at:new Date().toISOString(),applied:false}],limits:{
 enabled:false,workOrders:true,supplyOrders:false,taxChanges:false,treasuryIssues:false,actionsPerDay:3,spendingPerDay:100,
 maximumUnitPay:20,maximumUnits:10,maximumIssue:0,maximumTaxBasisPoints:0,taxStepBasisPoints:0,taxNoticeMinutes:0,taxCooldownMinutes:0}}
api.directorFinance=async()=>structuredClone(finance)
const receipts=new Map<string,number>()
let failFirstAcknowledgement=true
api.directorFinancePolicy=async(_slug,expectedVersion,limits,requestId)=>{
 if(receipts.has(requestId))return receipts.get(requestId)!
 if(expectedVersion!==finance.version)throw new Error('stale policy')
 finance.version++;finance.limits=structuredClone(limits);receipts.set(requestId,finance.version)
 if(failFirstAcknowledgement){failFirstAcknowledgement=false;throw new Error('Synthetic lost acknowledgement')}
 return finance.version
}
api.almightyVote=async(_slug,_id,accept)=>{view.proposals[0].status=accept?'Applied':'Rejected';view.proposals[0].result=accept?'Принято к сведению. Балансы не изменены.':'Отклонено царём.';return {result:view.proposals[0].result}}
createRoot(document.getElementById('app')!).render(<I18nProvider><div className="modal-backdrop"><section className="modal"><div className="modal-body"><AlmightyPanel slug="test"/></div></section></div></I18nProvider>)
