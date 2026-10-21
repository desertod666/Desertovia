using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Tests;

/// <summary>A deferred PostgreSQL trigger fails the actual final commit, after all action and accounting saves.</summary>
internal static class EconomyCommitFailure
{
    public static async Task<string> Install(DesertodDbContext db, params Guid[] requests)
        => await InstallFor(db,"EconomyReceipts",requests);

    public static async Task<string> InstallFor(DesertodDbContext db,string table,params Guid[] requests)
    {
        CheckTable(table);
        var name="reject_receipt_"+Guid.NewGuid().ToString("N");
        var ids=string.Join(",",requests.Select(id=>$"'{id}'::uuid"));
        // All identifiers and literals come from generated Guid values. No user SQL is accepted.
        var sql=$"CREATE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"RequestId\" IN ({ids}) THEN RAISE EXCEPTION 'Synthetic final commit failure'; END IF; RETURN NEW; END; $$; CREATE CONSTRAINT TRIGGER {name} AFTER INSERT ON \"{table}\" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION {name}();";
        await db.Database.ExecuteSqlRawAsync(sql);return name;
    }

    public static Task Remove(DesertodDbContext db,string name,string table="EconomyReceipts")
    {
        CheckTable(table);
        if(!name.StartsWith("reject_receipt_",StringComparison.Ordinal)||!Guid.TryParseExact(name[15..],"N",out _))
            throw new ArgumentException("Only this test's generated trigger can be removed.",nameof(name));
        var sql=$"DROP TRIGGER {name} ON \"{table}\"; DROP FUNCTION {name}();";
        return db.Database.ExecuteSqlRawAsync(sql);
    }
    private static void CheckTable(string table)
    {
        if(table is not ("EconomyReceipts" or "NotificationPreferenceReceipts" or "TreasuryIssues"))throw new ArgumentException("Unsupported fixture receipt table.",nameof(table));
    }
}
