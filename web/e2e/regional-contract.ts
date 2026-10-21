export interface RegionInspection {legacyMaskHash:string;legacySize:number;size:number;extraHash:string;milliseconds:number;reachable:boolean;waterBlocked:boolean;marshTiles:number;extraCounts:Record<string,number>}
declare global{interface Window{inspectRegions:(seed:number)=>Promise<RegionInspection>;atlasTravel:{x:number;y:number}[];atlasFocus:{x:number;y:number}[]}}
