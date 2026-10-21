/** World version2. Mirrors RegionalTerrain.Kind; no draws from the legacy map's random stream. */
export function regionalKind(seed:number,x:number,y:number,river:number):number{
 if(x>=river&&x<river+4)return 2
 const west=x<river
 if((west&&(x===14||x===15))||x===50||x===51||((y===92||y===93)&&x>=-26&&x<river-1)||((y===104||y===105)&&x>=45&&x<=66))return 14
 if(Math.abs(x-54)<=3&&Math.abs(y-104)<=3)return 3
 for(const [px,py] of [[-18,82],[-7,106],[63,83]]){
  const radius=((x-px)/4)**2+((y-py)/3)**2
  if(radius<=1)return 2
  if(radius<=2.8)return 11
 }
 if(!west&&y>=98&&y<=100&&x>=60&&x<=74&&x!==65&&x!==66)return 7
 if(west&&x>=3&&x<=12&&y>=89&&y<=97)return 5
 let value=(seed^Math.imul(x,0x27d4eb2d)^Math.imul(y,0x165667b1))>>>0
 value=Math.imul(value^(value>>>16),0x85ebca6b)>>>0;value=Math.imul(value^(value>>>13),0xc2b2ae35)>>>0;value=(value^(value>>>16))>>>0
 return value%20<6?1:value%20===6?4:value%20===7?3:0
}
