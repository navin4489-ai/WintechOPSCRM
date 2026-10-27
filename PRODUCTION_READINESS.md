# Production readiness — ongoing validation

## Verified in this pass

- API and Angular production builds succeed.
- 90 backend tests pass in Release, including checkout/accounting regression tests in
  `backend/ORO_POS.Tests/CheckoutTests.cs`.
- Checkout persists the platform's existing 5% GST policy, rounded to paise.
- Discounts update persisted GST/total; sales reports do not deduct discounts twice.
- Cash change is excluded from captured revenue; non-cash overpayments are rejected.
- Repeated payments on closed orders are rejected. Relational payment writes use a
  serializable transaction; PostgreSQL concurrency/load validation remains needed.
- Stock/recipe ingredient consumption posts on final settlement, not partial payments.
- Shift close and Z-report include captured cash and paid-in/out consistently.
- Table order lookup no longer depends on the first 50 orders in history.
- Legacy open orders quote the same tax calculation as checkout, without saving
  a read request or repricing closed historical orders.

## Live verification (27 October 2026)

- Angular bundle `main-2YUEHU2U.js` served successfully.
- Menu, inventory, reports and kitchen pages rendered; catalogue/stock APIs returned 200.
- QA takeaway order #17 (`QA checkout verification commit 7754b14`) was created
  separately from existing table orders: subtotal 600, discount 60, GST 27, total 567 INR.
- A 1000 INR cash tender captured 567 INR; stock for each of its two products moved 100 → 99.
- A second payment attempt returned 400, without posting another stock deduction.
- QA shift #3 was closed through the web UI with counted cash 567 INR. Both the
  stored shift and Z-report reported zero variance. This QA sale remains in reports;
  it is not a real customer sale and needs accounting reconciliation before go-live.

Tests use EF InMemory. They do not prove PostgreSQL locking behavior, gateway
integration, hardware printing, security isolation, or load capacity.

## Hardening and Android release pass

- Native Android 1.1.0 replaces the prototype with role-aware operations screens;
  13 Flutter/widget/PDF tests pass and `flutter analyze` reports no issues.
- Android 1.2.0 adds Super Admin outlet configuration and QR poster printing,
  working-outlet switching and gallery-based item image uploads.
- Android/web 1.3.0 outlet onboarding creates the outlet and its scoped admin
  account in one transaction. One-time client-side credential handoff is available;
  plaintext passwords are never included in server responses or audit values.
- Service-level tenant checks now supplement controller authorization. Two-outlet
  regression tests cover read and mutation denial across menu, stock, orders,
  kitchen, staff, outlet settings, reports, shifts and images. Missing list scope
  means the assigned outlet for tenant accounts, never all outlets.
- Outlet admins can manage **My Outlet** details/QR but only Super Admin can
  provision outlets. They cannot assign Super Admin or move themselves to a different outlet.
- Mobile web navigation uses a drawer, layouts stack on narrow viewports, and
  category creation is available on the catalogue page and within the item editor.
- The Playwright suite contains 21 browser/phone-width/print checks using synthetic API
  fixtures. These supplement, not replace, live PostgreSQL authorization checks.
- Customers can save a private tracking link and see preparation updates every
  15 seconds while the confirmation page is visible. No SMS/WhatsApp is sent.
- Wintech POS branding replaces the earlier visible platform name and logos.
  Receipts, bill previews, QR posters, sales and Z-report PDFs are outlet-only:
  bold centered business name, address and contact. QR layouts print no raw URL.
- Order/Z-report DTOs carry authoritative outlet identity. Print regression tests
  deliberately use an outlet different from the Super Admin profile to catch
  branding leakage between outlets. Receipt, sales, shift, QR and public
  confirmation PDF generation were checked; this is not physical-printer acceptance.
- Public, no-login QR ordering supports server-priced carts, retry-safe request IDs,
  private order-status proofs and cash-at-outlet collection by authenticated staff.
  Links can be disabled or rotated. Closed outlets or missing shifts reject new orders.
- QR checkout requires a validated customer contact number. It is included only
  in authenticated staff order notes, not the public confirmation/tracking DTO.
- Product photos use a dedicated PostgreSQL `ProductImages` table and additive EF
  migration. The API accepts PNG/JPEG/WebP up to 1 MB, checks file signatures and
  outlet permissions, audits changes and serves versioned cacheable image URLs.
- New gold/navy ORO branding and owner-approved private release signing, HTTPS-only
  Android networking, encrypted credential storage and no default-filled login.
- API actions re-read the account's active status, permissions and outlet assignment.
  Resource scope, role-escalation prevention, reason/manager approval, receipt audit,
  refund controls and list scoping are enforced server-side.
- Password rotation is available in web and Android. A password-derived security
  stamp revokes older sessions. All pre-upgrade tokens must sign in again.
- Fixed configuration precedence so environment secrets override JSON defaults.
  Production refuses to boot with the checked-in development JWT secret.
- Login rate limiting; production exceptions no longer return SQL/internal messages.
- Full external refunds with reason/reference, optional stock returns and cash payout
  recorded against the current shift. No bank calls are made.
- Web order history, receipts/refunds, audit viewer, non-table sale creation,
  quantity/modifier/notes entry, transfer controls, and real permission-checked CSV export.
- Kitchen routing no longer creates a new duplicate ticket for every added item;
  statuses advance in sequence and voids cancel active tickets.
- Inventory validates movements; physical counts post the difference, not the absolute count.
- New production databases require an initial admin password; demo seeding is opt-in
  (`SEED_DEMO_DATA=true`). `RESET_DB_ON_START` is false in the blueprint.
- Validation workflow added under `.github/workflows/validate.yml`. The initial
  clean-install failure exposed an inconsistent npm lockfile; it was repaired.
  The run for `d4f1631` completed successfully for API, web and Android UI checks.
- `scripts/backup-database.ps1` provides an isolated `oro_pos` dump path; it has not
  been executed or restore-tested and is not an automatic backup service.

## Owner decisions

- Use an existing external card terminal/UPI collection app. Automatic gateway
  capture is not part of this release.
- Keep the current free Render environment **for testing**. No billing upgrade or
  production-hosting purchase was authorized or performed.

## QR and item-image live checks

- Main Outlet QR ordering was enabled, and its PNG was saved locally at
  `mobile/releases/Main-Outlet-QR.png`. Download the current code from Super Admin
  again if the link is rotated; generated outlet QR files are not committed.
- Anonymous menu returned 20 available products with cash-at-outlet instructions.
- Browser cart validation showed 280 INR subtotal, 14 INR GST and 294 INR total for
  Paneer Tikka, and blocked checkout without the customer's name.
- QA guest order #19 used server pricing despite a client-supplied zero price.
  Repeating its request ID returned the same order. An unauthenticated attempt to
  pay returned 401. The unpaid QA order was immediately voided; no cash or stock moved.
- Product #21 (`QA image validation - not for sale`) was created unavailable for
  media tests. Upload through the web item section rendered a 392×392 thumbnail;
  anonymous image GET returned PNG, and an ETag request returned 304. The QA image
  was removed after testing; the unavailable QA item remains as an audit/test record.
- No real product photo was overwritten, and no existing user's cash shift was
  opened, closed or changed during these checks.

## Release blockers / remaining acceptance work

- Perform a full role-by-role authorization and outlet-isolation review of every API,
  especially status changes, voids, table operations, reports, shifts and inventory.
- Complete end-to-end acceptance of the new refund, receipt, audit viewer, table
  operation and export flows. The original implementation checklist overstates
  coverage: supplier/PO modules, attendance scheduling/corrections, configurable
  service charges, peripheral management and offline replay
  are not delivered as complete modules.
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
