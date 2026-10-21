import type {WalkJourney} from '../api'

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
