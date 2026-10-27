# Wintech POS Android 1.4.0

Native Flutter workspace with the gold/navy Wintech W mark. Uses the same API and INR
accounting as the web app, not an embedded web page.

## Available workflows

- Secure sign-in, encrypted token storage, expiry handling, sign-out and password change.
- Role-aware navigation and assigned-outlet scope (also enforced by the API).
- Dashboard, table ordering, takeaway/quick-service/retail sales, catalogue/category search,
  keyboard/USB barcode input, quantity, kitchen notes and modifier selections.
- Resume existing table bills, remove eligible items, discounts/voids with reason and
  manager approval when needed; transfer, merge and single-line split to a free table.
- Cash change, Card/UPI tender selection, remaining balances and settled receipt printing.
- Paginated order history, audited receipt/reprint and full external refund recording,
  with an explicit physical-stock-return choice.
- Kitchen station tickets, preparation states, delayed-ticket highlighting and refresh.
- Open/close cash shifts, opening float, counted cash, variance and Z-report viewing.
- Stock, ledger, receipts/waste/signed adjustments/physical counts, recipe mapping.
- Catalogue/category/table creation, staff create/edit/deactivation, audit history.
- Owner/Super Admin outlet configuration, working-outlet selection, public QR ordering
  enable/disable, link copy and QR poster printing.
- Super Admin outlet creation provisions a separate outlet-admin username/password
  atomically. Login details can be revealed/copied once for secure handoff; the
  server stores only a password hash. Email is optional for this onboarding flow.
- Outlet administrators (`StoreManager`) have a **My Outlet** screen and cannot
  switch to another outlet workspace or create additional outlets.
- Item image upload/replacement from the Android gallery. Images are stored in
  PostgreSQL and displayed on POS, catalogue and the public QR menu.
- Filtered sales reports and Android print/PDF report output.

Card/UPI payments and refunds are collected/returned **externally**, as selected by
the owner. Recording a transaction here never calls a bank or a merchant gateway.

The earlier prototype customer QR/order-status placeholders and unauthenticated
waiter/KDS screens have been replaced. Customers scan the outlet QR and order in
their mobile browser without installing the staff app or signing in. Cash is
collected later by signed-in staff. Purchase orders/suppliers, staff scheduling/attendance correction, service charges, printer
device management and fully automated offline replay are not implemented modules.
See `../PRODUCTION_READINESS.md` for release acceptance work.

## Build and validate

```powershell
flutter pub get
flutter analyze
flutter test
flutter build apk --release
```

API default: `https://the-bathany.onrender.com/api`. Override only at build time
with `--dart-define=API_URL=https://your-host/api`. Release Android blocks cleartext HTTP.

Output: `build/app/outputs/flutter-apk/app-release.apk`.

## Branding and customer documents

- Application and launcher branding is **Wintech POS**, using the gold/navy W mark.
- Receipts, bill previews, sales PDFs, Z-report PDFs and ordering QR posters use
  **only the relevant outlet's identity**: bold centered name, address, contact.
- Receipt/shift branding comes from the actual order/shift outlet returned by the
  server, not a cached operator profile. Sales/QR printing fetches the selected
  outlet's current details. Missing outlet metadata blocks printing rather than
  accidentally using another outlet or a platform label.
- No ordering URL or platform footer is printed beneath the QR code. Copy link
  remains available separately in outlet administration.
- Existing internal package/session identifiers and the private signing key are
  retained for compatibility with installed versions; these are not printed branding.

## Signing — IMPORTANT

A new private release key was generated with owner approval. It is **not** the
Flutter debug key. These files are local and excluded from Git:

- `android/keystore/oro-pos-release.jks`
- `android/key.properties` (contains private signing passwords)

Back up **both** to encrypted, access-controlled storage before distributing the
APK. Losing them can prevent future updates. Do not send them alongside the APK,
commit them, or paste their contents into tickets/chat. A clean checkout must
restore these private files before a release build; it will not silently use a
debug signature. For Play publishing, build an AAB using the same key and configure
Play App Signing in your own developer account.

Earlier debug-signed APKs cannot be updated in place with this release key. If
Android reports a signature conflict, uninstall the earlier test app first, then
install this release. Orders are on the server; local sign-in state is removed.

No Android device/emulator was connected during this build. Widget tests cover
phone layout, login validation, outlet selection, table resumption, reason capture
and actual UPI tender submission. Physical installation, Android Keystore storage,
print services, scanners and network-recovery testing remain acceptance tasks.
