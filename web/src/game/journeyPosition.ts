import type {WalkJourney} from '../api'

export interface JourneySample { value:WalkJourney;receivedAt:number }

/** Reapplying an unchanged server snapshot must not restart its local clock. */
export function receiveJourney(old:JourneySample|null|undefined,value:WalkJourney|null|undefined,receivedAt:number):JourneySample|null {
 if(!value)return null
 if(old){
  const previous=old.value
  if(Date.parse(value.snapshotAt)<Date.parse(previous.snapshotAt))return old
  if(previous===value||previous.snapshotAt===value.snapshotAt&&previous.id===value.id
   &&previous.setOutAt===value.setOutAt&&previous.arrivesAt===value.arrivesAt
   &&previous.blockedAt===value.blockedAt&&previous.blockReason===value.blockReason
   &&previous.speedMultiplier===value.speedMultiplier&&previous.from.x===value.from.x&&previous.from.y===value.from.y
   &&previous.waypoints.length===value.waypoints.length&&previous.waypoints.every((p,i)=>p.x===value.waypoints[i].x&&p.y===value.waypoints[i].y&&p.cost===value.waypoints[i].cost))return old
 }
 return{value,receivedAt}
}

/** Render the persisted server path against its snapshot clock, including slower marsh segments. */
export function journeyPosition(journey:WalkJourney,elapsedSinceSnapshot:number){
 const sampledNow=Date.parse(journey.snapshotAt)+Math.max(0,elapsedSinceSnapshot)
 const now=journey.blockedAt?Math.min(sampledNow,Date.parse(journey.blockedAt)):sampledNow
 let remaining=Math.max(0,(now-Date.parse(journey.setOutAt))/1000)*Math.max(.1,journey.speedMultiplier)/.6
 let from=journey.from
 for(const step of journey.waypoints){
  const length=Math.hypot(step.x-from.x,step.y-from.y)*step.cost
  const share=length<=0?1:Math.max(0,Math.min(1,remaining/length))
  const position={x:from.x+(step.x-from.x)*share,y:from.y+(step.y-from.y)*share}
  if(remaining<length)return{...position,moving:now>=Date.parse(journey.setOutAt)&&(!journey.blockedAt||sampledNow<Date.parse(journey.blockedAt))}
  remaining-=length;from=step
 }
 return{x:from.x,y:from.y,moving:false}
}
