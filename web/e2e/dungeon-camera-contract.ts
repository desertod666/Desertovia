export interface DungeonCameraStand {
  read(): { scale: number; tilePixels: number; following: boolean; x: number; y: number }
  point(x: number, y: number): { x: number; y: number }
  hero(x: number, y: number): void
}
declare global { interface Window { dungeonCameraStand: DungeonCameraStand; dungeonCameraOrders: string[] } }
