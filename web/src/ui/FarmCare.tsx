import type {FarmCare as Care} from '../api'
import {useI18n} from '../i18n'

export function FarmCare({actions}:{actions:Care[]}) {
 const i18n=useI18n(),ru=i18n.language==='ru'
 const names={Sow:ru?'посадка':'sowing',Water:ru?'полив':'watering',Fertilize:ru?'удобрение':'fertilizing',Harvest:ru?'сбор':'harvest'}
 const people=new Map<string,{name:string;steps:Map<Care['step'],number>}>()
 for(const action of actions){let person=people.get(action.accountId);if(!person){person={name:action.name,steps:new Map()};people.set(action.accountId,person)}person.steps.set(action.step,(person.steps.get(action.step)??0)+1)}
 return <ul className="farm-care-list">{[...people.entries()].map(([id,person])=><li key={id}><strong>{person.name}</strong> · {[...person.steps].map(([step,count])=>`${names[step]}${count>1?` ×${count}`:''}`).join(', ')}</li>)}</ul>
}
