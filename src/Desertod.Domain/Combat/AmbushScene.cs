using Desertod.Domain.Catalog;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

/// <summary>A short manually controlled world encounter, using the existing dungeon movement and checkpoint engine.</summary>
public static class AmbushScene
{
    public static DungeonDefinition Map(EnemyKind kind)
    {
        var enemy=EnemyCatalog.Get(kind);
        if(enemy.DungeonOnly)throw new GameRuleException("ambush.enemy","This enemy does not ambush workers.");
        const int width=12,height=9;
        var tiles=Enumerable.Range(0,width*height).Select(i=>
            i%width is 0 or width-1||i/width is 0 or height-1?DungeonTile.DeadTree:DungeonTile.Ground).ToArray();
        tiles[4*width]=DungeonTile.Entrance;
        tiles[2*width+5]=DungeonTile.Rock;tiles[6*width+7]=DungeonTile.DeadTree;
        return new()
        {
            Id="ambush-"+kind.ToString().ToLowerInvariant(),Name="Forest encounter",Width=width,Height=height,
            Tiles=tiles,Entrance=new TilePoint(.5,4.5),Spawns=[new(kind,8,4)],TimeLimit=TimeSpan.FromSeconds(60),
            // Initial encounter balance: no second loot table. The accepted work settlement will own the reward.
            FoeDefinitions=new Dictionary<EnemyKind,DungeonFoe>{[kind]=new(kind,enemy.Power*6,enemy.Power,1.25,1.7,12,.9,0,[])},
        };
    }

    public static DungeonFighter Fighter(Citizen citizen)
    {
        var weapon=CombatRules.UsableWeapon(citizen)?.Kind;
        var role=weapon switch{ResourceKind.Bow=>HeroClass.Archer,ResourceKind.Spear=>HeroClass.Spearman,ResourceKind.Sword=>HeroClass.Swordsman,_=>HeroClass.Brawler};
        var definition=HeroClassCatalog.Get(role);
        return new(citizen.Id,citizen.AccountId,citizen.Name,role,DungeonRules.Health(citizen,definition),
            DungeonRules.Damage(citizen,definition),ArmoryCatalog.Armor(citizen.Armor)?.Protection??0,
            role==HeroClass.Archer?citizen.Arrows:null,false);
    }

    public static DungeonSimulation Create(Citizen citizen,EnemyKind kind)=>new(Map(kind),[Fighter(citizen)]);
}
