using System.Text.Json;
using System.Text.Json.Nodes;
using Desertod.Api.Game;

namespace Desertod.Api.Tests;

public sealed class DirectorSnapshotBudgetTests
{
    [Fact]
    public void Ordinary_snapshot_keeps_its_exact_stored_form()
    {
        const string value="""{"facts":{"online":3},"economy":{"analysis":{"allResidents":true}},"options":[]}""";
        Assert.Equal(value,AlmightyAnalysis.BoundSnapshot(value));
    }

    [Fact]
    public void Descriptive_context_can_shrink_but_admission_and_every_economic_row_remain_exact()
    {
        var root=JsonNode.Parse("""{"contract":{"revision":"unchanged"},"permissions":[{"kind":"advice"}],"facts":{"online":3},"options":[{"id":"economy_report"}],"economy":{"analysis":{"completeDays":{"columns":["day","profit"],"rows":[["2026-10-20",220]]}}}}""")!;
        root["previous"]=new JsonArray(Enumerable.Range(0,4).Select(_=>(JsonNode)new JsonObject{["reason"]=new string('я',1200),["result"]=new string('я',500)}).ToArray());
        root["catalog"]=new JsonObject{["recipes"]=new string('x',36000),["note"]="Descriptions never grant tools."};
        var result=JsonNode.Parse(AlmightyAnalysis.BoundSnapshot(root.ToJsonString()))!;
        foreach(var field in new[]{"contract","permissions","facts","options","economy"})Assert.True(JsonNode.DeepEquals(root[field],result[field]),field+" changed");
        Assert.NotNull(result["contextOmissions"]);Assert.Null(result["catalog"]!["recipes"]);
        Assert.InRange(result.ToJsonString().Length,1,AlmightyAnalysis.SnapshotCharacterLimit);
    }

    [Fact]
    public void Required_data_that_still_exceeds_budget_is_not_silently_sampled_or_dropped()
    {
        var root=new JsonObject{["economy"]=new JsonObject{["analysis"]=new string('x',37000)},["options"]=new JsonArray(new JsonObject{["id"]="none"})};
        var result=AlmightyAnalysis.BoundSnapshot(root.ToJsonString());
        Assert.True(result.Length>AlmightyAnalysis.SnapshotCharacterLimit);
        Assert.True(JsonNode.DeepEquals(root["economy"],JsonNode.Parse(result)!["economy"]));
    }
}
