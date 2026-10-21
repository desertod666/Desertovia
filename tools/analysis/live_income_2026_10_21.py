"""Read-only live aggregates; emits no account IDs, names or credentials."""
import json
import pathlib
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[2]
SQL = r'''
BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '15s';
WITH
days AS (SELECT generate_series('2026-10-16'::date,'2026-10-20'::date,'1 day')::date d),
w AS (
 SELECT ("At" AT TIME ZONE 'UTC')::date d,"AccountId" a,sum("Delta") income,count(*) cycles
 FROM "LedgerEntries" WHERE "Operation"='wage' AND "Asset"='DZK' AND "Location"='wallet'
 GROUP BY 1,2
),
t AS (
 SELECT ("ClosedAt" AT TIME ZONE 'UTC')::date d,"SellerAccountId" a,
 sum("Price") revenue,sum("CostBasis") basis,
 sum(greatest("Price"-"CostBasis",0)) gain,
 sum("Price"-"CostBasis") net,
 sum(greatest("Price"-"CostBasis",0)) FILTER (WHERE "CostBasis">0) positive_basis_gain,
 count(*) sales
 FROM "MarketLots" WHERE "Status"='sold' GROUP BY 1,2
),
full_w AS (SELECT w.* FROM w JOIN days USING(d)),
full_t AS (SELECT t.* FROM t JOIN days USING(d)),
tax AS (
 SELECT *, (least(greatest(gain-200,0),300)*0.10 + least(greatest(gain-500,0),500)*0.20 + greatest(gain-1000,0)*0.30) tax
 FROM t
),
daily AS (
 SELECT d,
 (SELECT count(*) FROM w WHERE w.d=days.d) wage_receivers,
 (SELECT sum(income) FROM w WHERE w.d=days.d) wages,
 (SELECT percentile_cont(0.5) WITHIN GROUP(ORDER BY income) FROM w WHERE w.d=days.d) wage_median,
 (SELECT count(*) FROM t WHERE t.d=days.d) sellers,
 (SELECT sum(sales) FROM t WHERE t.d=days.d) sales,
 (SELECT sum(revenue) FROM t WHERE t.d=days.d) sales_revenue,
 (SELECT sum(gain) FROM t WHERE t.d=days.d) recorded_positive_margin,
 (SELECT max(gain) FROM t WHERE t.d=days.d) max_seller_positive_margin,
 (SELECT count(*) FROM tax WHERE tax.d=days.d AND gain>200) taxed_sellers,
 (SELECT sum(tax) FROM tax WHERE tax.d=days.d) hypothetical_tax_exact,
 (SELECT sum(floor(tax)) FROM tax WHERE tax.d=days.d) hypothetical_tax_whole
 FROM days
),
wstats AS (
 SELECT count(*) player_days,count(DISTINCT a) unique_receivers,sum(income) total,
 round(avg(income),2) mean,
 percentile_cont(ARRAY[0.1,0.25,0.5,0.75,0.9,0.95]) WITHIN GROUP(ORDER BY income) percentiles_10_25_50_75_90_95,
 min(income) min,max(income) max,
 count(*) FILTER(WHERE income<=200) at_most_200,
 count(*) FILTER(WHERE income>500) above_500,
 count(*) FILTER(WHERE income>1000) above_1000 FROM full_w
),
tstats AS (
 SELECT count(*) seller_days,count(DISTINCT a) unique_sellers,sum(sales) sales,sum(revenue) revenue,
 sum(basis) recorded_basis,sum(gain) recorded_positive_margin,sum(net) recorded_net_margin,
 sum(positive_basis_gain) positive_basis_only_margin,
 round(avg(gain),2) mean,
 percentile_cont(ARRAY[0.25,0.5,0.75,0.9,0.95]) WITHIN GROUP(ORDER BY gain) percentiles_25_50_75_90_95,
 max(gain) max,count(*) FILTER(WHERE gain>200) above_200,
 count(*) FILTER(WHERE gain>500) above_500,count(*) FILTER(WHERE gain>1000) above_1000 FROM full_t
)
SELECT json_build_object(
 'captured_at',now(),'database',current_database(),'period_utc',json_build_object('from','2026-10-16T00:00:00Z','until_exclusive','2026-10-21T00:00:00Z'),
 'ledger_coverage',(SELECT json_build_object('rows',count(*),'first',min("At"),'last',max("At")) FROM "LedgerEntries"),
 'population',(SELECT json_build_object('citizens',count(*),'accounts',count(DISTINCT "AccountId")) FROM "Citizens"),
 'worlds',(SELECT count(*) FROM "Villages"),
 'wages',(SELECT row_to_json(wstats) FROM wstats),
 'wage_units',(SELECT json_agg(x ORDER BY unit) FROM (SELECT "Delta" unit,count(*) payments FROM "LedgerEntries" WHERE "Operation"='wage' AND "Asset"='DZK' AND "Location"='wallet' AND "At">='2026-10-16T00:00:00Z' AND "At"<'2026-10-21T00:00:00Z' GROUP BY 1) x),
 'trade_ledger_crosscheck',(SELECT json_build_object('credits',count(*) FILTER(WHERE "Delta">0),'credit_sum',sum("Delta") FILTER(WHERE "Delta">0),'debits',count(*) FILTER(WHERE "Delta"<0),'debit_sum',sum("Delta") FILTER(WHERE "Delta"<0)) FROM "LedgerEntries" WHERE "Operation"='trade' AND "Asset"='DZK' AND "Location"='wallet' AND "At">='2026-10-16T00:00:00Z' AND "At"<'2026-10-21T00:00:00Z'),
 'trade',(SELECT row_to_json(tstats) FROM tstats),
 'daily',(SELECT json_agg(daily ORDER BY d) FROM daily),
 'full_period_trade_basis',(SELECT json_build_object('zero_basis_sales',count(*) FILTER(WHERE "CostBasis"=0),'positive_basis_sales',count(*) FILTER(WHERE "CostBasis">0),'zero_basis_revenue',sum("Price") FILTER(WHERE "CostBasis"=0),'positive_basis_revenue',sum("Price") FILTER(WHERE "CostBasis">0)) FROM "MarketLots" WHERE "Status"='sold' AND "ClosedAt">='2026-10-16T00:00:00Z' AND "ClosedAt"<'2026-10-21T00:00:00Z'),
 'positive_basis_sensitivity',(SELECT json_build_object('seller_days',count(*),'above_200',count(*) FILTER(WHERE positive_basis_gain>200),'maximum',max(positive_basis_gain),'tax_exact',sum(least(greatest(positive_basis_gain-200,0),300)*0.10+least(greatest(positive_basis_gain-500,0),500)*0.20+greatest(positive_basis_gain-1000,0)*0.30)) FROM full_t),
 'tax_full_period',(SELECT json_build_object('exact',sum(tax),'whole_units',sum(floor(tax)),'taxed_seller_days',count(*) FILTER(WHERE gain>200),'unique_taxed_sellers',count(DISTINCT a) FILTER(WHERE gain>200)) FROM tax JOIN days USING(d)),
 'alternative_half_rates',(SELECT json_build_object('exact',sum(tax/2),'whole_units',sum(floor(tax/2)),'rates_percent',json_build_array(0,5,10,15),'same_thresholds',true) FROM tax JOIN days USING(d)),
 'top_seller_days_anonymous',(SELECT json_agg(x) FROM (SELECT d,revenue,basis,gain,net,sales,tax,round(100*tax/nullif(gain,0),2) effective_percent FROM tax JOIN days USING(d) ORDER BY gain DESC LIMIT 10) x),
 'today_partial',(SELECT json_build_object('day_utc','2026-10-21','wage_receivers',count(*),'wages',sum(income),'median',percentile_cont(0.5) WITHIN GROUP(ORDER BY income)) FROM w WHERE d='2026-10-21'),
 'today_trade_partial',(SELECT json_build_object('seller_days',count(*),'sales',sum(sales),'revenue',sum(revenue),'recorded_positive_margin',sum(gain),'maximum',max(gain),'tax_exact',sum(tax),'tax_whole',sum(floor(tax))) FROM tax WHERE d='2026-10-21'),
 'wallet_day_warning',(SELECT json_build_object('wallets',count(*),'stale_income_days',count(*) FILTER(WHERE "IncomeDay"<current_date),'coins_total',sum("Coins")) FROM "GameWallets")
);
COMMIT;
'''

run = subprocess.run(['ssh','oracle-vps',
 'sudo -n docker exec -i dezertovia-postgres-1 psql -X -qAt -v ON_ERROR_STOP=1 -U desertod -d desertod'],
 input=SQL, text=True, encoding='utf-8', capture_output=True, check=True)
data = json.loads(run.stdout)
assert data['wages']['total'] == sum(x['wages'] for x in data['daily'])
assert data['trade']['sales'] == data['trade_ledger_crosscheck']['credits']
assert data['trade']['revenue'] == data['trade_ledger_crosscheck']['credit_sum']
assert data['trade_ledger_crosscheck']['credit_sum'] + data['trade_ledger_crosscheck']['debit_sum'] == 0
assert data['wages']['total'] == sum(x['unit']*x['payments'] for x in data['wage_units'])
data['method'] = {
 'mode':'single repeatable-read READ ONLY transaction, 15s statement timeout',
 'source_container':'dezertovia-postgres-1',
 'excluded_synthetic_container':'desertovia-foundation-dev-postgres',
 'deployed_image_commit':'4bb125f2dc05711ebaf4dac2723f3ee19eae3ea9',
 'day_boundary':'UTC, matching deployed EconomyDay; 00:00 UTC = 02:00 Europe/Oslo on these dates',
 'privacy':'only aggregate totals and anonymous seller-day amounts; no account IDs, names or credentials',
 'rates_status':'hypothetical, NOT approved or activated',
 'legacy_basis_warning':'live schema has no CostBasisKnown: recorded margins are not proven economic profit and must not become retrospective tax bills',
 'hypothetical_scale': [[0,0],[200,0.10],[500,0.20],[1000,0.30]],
 'gain_formula':'sum(max(sale Price - recorded CostBasis, 0)) per seller UTC day; losses do not refund prior tax',
 'rounding':'fractional tax retained within seller-day; whole-unit tax floors once at seller-day level'
}
path = ROOT/'docs/dezertovia/LIVE_INCOME_ANALYSIS_2026_10_21.json'
path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(data,ensure_ascii=False,indent=2))
