using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.World;

namespace Desertod.Domain.Rules;

/// <summary>
/// A spot the player picked on the map for gathering: a tree, a berry bush, a stone, the reeds on the bank.
/// The server knows only the trees of the camp land; other spots are where the citizen is seen working,
/// so they never make the walk shorter than from the task's own workplace.
/// </summary>
public static class WorkSpots
{
    /// <summary>Workplaces whose goods can be gathered anywhere on the land, not only at the workplace itself.</summary>
    public static IReadOnlySet<string> Workplaces { get; } = new HashSet<string> { Forest.Workplace, "stones", "berries", "reeds" };

    public static bool Takes(TaskDefinition task) =>
        task.Effect == TaskEffect.Resources && task.Hunt is null && task.Inputs.Count == 0 && Workplaces.Contains(task.Workplace);

    /// <summary>The standing camp tree on the spot's tile, if the task chops wood there.</summary>
    public static Tree? TreeAt(Village village, TaskDefinition task, TilePoint spot, DateTimeOffset now)
    {
        if (task.Workplace != Forest.Workplace)
        {
            return null;
        }

        var tile = spot.ToTile();
        return Forest.Visible(village, now).FirstOrDefault(tree => tree.X == tile.X && tree.Y == tile.Y);
    }

    public static void Check(Village village, TaskDefinition task, TilePoint spot, DateTimeOffset now)
    {
        if (!Takes(task))
        {
            throw new GameRuleException("spot.notForTask", $"\"{task.Name}\" is done at its own place.",
                new Dictionary<string, object> { ["taskId"] = task.Id });
        }

        BankAccess.Require(village, spot);
        if(RegionalTerrain.NewLand(spot))
        {
            var kind=RegionalTerrain.For(village.Id,village.Wilds)[RegionalTerrain.Index((int)Math.Floor(spot.X),(int)Math.Floor(spot.Y))];
            var expected=task.Workplace switch {Forest.Workplace=>1,"stones"=>3,"berries"=>4,"reeds"=>5,_=>-1};
            if(kind!=expected)throw new GameRuleException("spot.notForTask","This resource is not found on the selected tile.",new Dictionary<string,object>{["taskId"]=task.Id});
        }
        var tile = spot.ToTile();
        if (!WorldMap.Land.Contains(tile))
        {
            throw new GameRuleException("spot.outsideLand", "This spot is beyond the land.");
        }

        if (task.Workplace == Forest.Workplace && Forest.Area.Contains(tile) && TreeAt(village, task, spot, now) is null)
        {
            throw new GameRuleException("spot.noTree", "No tree stands here any more.");
        }
    }
}
