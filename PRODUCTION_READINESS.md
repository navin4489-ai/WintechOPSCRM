# Production readiness — ongoing validation

## Verified in this pass

- API and Angular production builds succeed.
- 15 backend tests pass, including checkout/accounting regression tests in
  `backend/ORO_POS.Tests/CheckoutTests.cs`.
- Checkout persists the platform's existing 5% GST policy, rounded to paise.
- Discounts update persisted GST/total; sales reports do not deduct discounts twice.
- Cash change is excluded from captured revenue; non-cash overpayments are rejected.
- Repeated payments on closed orders are rejected. Relational payment writes use a
  serializable transaction; PostgreSQL concurrency/load validation remains needed.
- Stock/recipe ingredient consumption posts on final settlement, not partial payments.
- Shift close and Z-report include captured cash and paid-in/out consistently.
- Table order lookup no longer depends on the first 50 orders in history.

Tests use EF InMemory. They do not prove PostgreSQL locking behavior, gateway
integration, hardware printing, security isolation, or load capacity.

## Release blockers / remaining acceptance work

- Perform a full role-by-role authorization and outlet-isolation review of every API,
  especially status changes, voids, table operations, reports, shifts and inventory.
- Implement and verify refund, receipt, audit viewer, table operations and report
  export flows against the complete BRD. CSV export currently contains a placeholder.
- Card/UPI are recorded tender types, not confirmed gateway/bank transactions.
  Configure an actual provider and webhook reconciliation if online capture is required.
- Confirm the business's GST applicability, tax classes and rates with its accountant;
  5% is the existing application policy, not a tax-compliance certification.
- Reconcile older test transactions: earlier builds displayed tax without storing it
  and did not post stock consumption. Do not silently rewrite closed financial records.
- Test multiple clients, network interruption, concurrent checkout and recovery on PostgreSQL.
- Add end-to-end browser regression coverage, real printer testing and mobile regression testing.
- Rotate the seeded admin password and deploy secrets before real operation; remove
  demo catalogue/opening stock or reconcile it with the real menu and physical count.
- Provision durable hosting/database, automatic backups and a tested restore process.
  Current free Render hosting can sleep and the free database has expiry/backup limitations.

The application is **not yet certified production-ready**. Passing builds and the
checkout tests are an incremental improvement, not completion of the full BRD.
