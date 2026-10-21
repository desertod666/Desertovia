export interface JourneyCommand{requestId:string;journeyId:string;blockedAt:string}
declare global{interface Window{journeyRequests:JourneyCommand[];journeyAtlasOpened:boolean;journeyCancelled:boolean}}
