import { createRoot } from 'react-dom/client'
import { api, type AlmightyView } from '../src/api'
import { I18nProvider } from '../src/i18n'
import { AlmightyPanel } from '../src/ui/AlmightyPanel'
import '../src/index.css'
const view: AlmightyView = { enabled:true, state:{mode:'Propose',policyVersion:0,disabledKinds:[],intent:'Сначала восстановить охрану и оценить запасы, затем предложить событие.',lastReviewAt:new Date().toISOString(),nextReviewAt:new Date(Date.now()+900000).toISOString(),restUntil:null,lastError:'',callsToday:1,lastSnapshot:'{}'},
 proposals:[{id:'one',option:'economy_report',reason:'Я вижу что городу нужны доски и ремонт. Предлагаю согласовать работы, сохранив медленный темп игры.',intent:'Наблюдать',status:'Pending',createdAt:new Date().toISOString(),expiresAt:new Date(Date.now()+1800000).toISOString(),noticeState:'Sent',result:''}], observations:[] }
api.almighty=async()=>structuredClone(view)
api.almightyMode=async(_slug,mode)=>{view.state.mode=mode}
api.almightyVote=async(_slug,_id,accept)=>{view.proposals[0].status=accept?'Applied':'Rejected';view.proposals[0].result=accept?'Принято к сведению. Балансы не изменены.':'Отклонено царём.';return {result:view.proposals[0].result}}
createRoot(document.getElementById('app')!).render(<I18nProvider><div className="modal-backdrop"><section className="modal"><div className="modal-body"><AlmightyPanel slug="test"/></div></section></div></I18nProvider>)
