# Dcoin: owner adjustments and local shop goods ·21 October2026

Partial T060 over the verified1174e08 wallet foundation. Source and synthetic
tests only; no public installation, model calls, real Telegram messages or
payments. The bot's existing candles KV remains canonical. Gold/TEST_CANDLE,
legacy prices and rank thresholds are unchanged.

## Implemented boundary

- The existing owner/session/CSRF-protected manual-adjustment form adds a fresh
  operation nonce. Retrying that form returns its original actual adjustment,
  without crediting/spending twice or repeating a rank announcement. A changed
  recipient/amount conflicts. A fresh form permits an intentional new operation.
  The response distinguishes the balance after that operation from today's
  balance. Legacy clamped deductions retain their actual debit in the receipt.
- `dcoin_shop.purchase` delivers titles and indulgences under the wallet writer
  transaction: debit, goods and immutable business outcome commit together or
  roll back. SQLite independent connections serialize both insufficient-money
  races and attempts to buy a second unconsumed indulgence. Consuming an
  indulgence uses a conditional atomic update.
- The shop receipt binds buyer, item, normalized title and the displayed price.
  It stores the accepted server price. Changed terms conflict; an outdated
  quoted price rejects. A replay returns the original outcome even after the
  catalog price changes. It neither restores an old title over a new one nor
  reissues a consumed indulgence. Insufficient/rejected requests stay rejected.
- Telegram commands use chat/message identity. Confirmation buttons persist
  their price and use a32hex nonce in a user-scoped KV key; their data survives
  failed writes and successful retries. Older price-less buttons require a new
  confirmation rather than guessing the price the player saw.
- The existing Prihod page persists an approved local-goods request before
  sending it. Lost responses/page reopen reuse its original operation/terms;
  only an authenticated matching-user API acknowledgment clears it. Storage
  is separated by user. Storage failure blocks sending an unrecoverable order.
  Old web clients missing an operation/quote must refresh the page. This is a
  functional request-lifecycle change, not the deferred graphical redesign.
- Profile erasure removes the new user-linked order history as well as wallet
  history. Only unlinked keyed hashes of retired request identities remain,
  without raw request/user IDs, amounts, goods or names. They reject old retries
  against a recreated profile. A DB-local salt is created lazily on erasure;
  normal startup still rewrites no existing wallet/KV rows.

The additive tables are `dcoin_shop_orders` and `dcoin_retired_requests`.
The prior wallet opening/receipt tables and all old application tables remain.

## Verification and limits

`tests/test_dcoin_commands.py` covers real SQLite contention, restart/replay,
changed buyer/title/item/price, insufficient/outdated quote replay, consumed
goods, injected goods/order-write rollback, erasure/recreated-profile replay,
owner authentication/CSRF/nonce and rank-announcement count, HTTP requests and
Telegram command/confirmation retry. Node executes the embedded browser request
store through lost-response/reload/account/storage cases and parses the actual
page JavaScript. The expanded local run passed54tests in45.716seconds, including
the prior wallet/upgrade/Prihod/admin/shop regressions. Exact checks are in
`DCOIN_COMMANDS_VERIFICATION_1_0.json`; full CI is a separate gate.

Blessing and roast external delivery still need durable preparation/outbox,
uncertain-send/restart/reconciliation and semantic request IDs. Their current
paths now check the atomic debit before generation/delivery; an old roast
refund no longer accrues rank. These guards do not make external sending
exactly-once. Provider events, weekly/other legacy reward identities, the full
reward budget, payment/refund contracts, entitlements, wallet naming/cutover and
authorized full-data migration remain open under the original DAG. The full
T060/T065 or1.0 is not marked complete by this slice.

Exact bot40ea4e7 passed CI39637536510:1154tests in219.308seconds; server skipped.
This is the scoped source gate, not a1.0 release. The next task must follow the
original source-plan/acceptance reconciliation recorded in GOAL_1_0.md and
EXECUTION_SEQUENCE_1_0.md; the previously suggested T033 is not an automatic next task.
