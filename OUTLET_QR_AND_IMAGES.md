# Outlet administration, customer QR ordering and product images

## Super Admin / Owner

1. Sign in with an **Owner** account (this is the platform's Super Admin role).
2. Open **Super Admin · Outlets** on web, or **Super Admin** in Android navigation.
3. Create/edit the outlet name, address, phone, timezone and operating status.
4. Choose **Enable & generate QR**. Download the PNG on web or print a QR poster
   from Android. Copy the ordering link if needed.
5. To stop new public orders, disable ordering or close the outlet. Rotating a link
   revokes the old QR code; replace all printed posters afterward.
6. Use **Use this outlet workspace** to manage a different outlet as Owner. Cash
   shifts remain outlet-specific; close an old shift before opening another.

Only Owner accounts can configure outlets and QR links. An outlet manager cannot
escalate to platform administration merely by calling the endpoint directly.

## Outlet item images

- Web: **Menu & Catalogue → Add item / Edit → Save**, then **Upload image** on the row.
- Android: **Catalogue → item three-dot menu → Upload image**, choose from the gallery.
- Outlet administrators (`StoreManager`) or holders of `ManageSettings` can upload/replace/remove
  photos for their own outlet only. Owner can administer every outlet.
- Supported uploads: PNG, JPEG or WebP, maximum **1 MB**. SVG/documents are rejected.
- Photos are saved in PostgreSQL, not the ephemeral Render filesystem. Database
  backups include image bytes. Large catalogues need appropriate database capacity.
- The saved photo appears on web POS, Android POS/catalogue and the public QR menu.
  Products without photos show a deliberate placeholder until an admin uploads one.

## Customer flow — no staff login, no app installation

1. Scan the outlet QR using the phone's camera.
2. Browse images/categories, choose quantities/modifiers and add kitchen notes.
3. Enter a pickup name and contact number, then submit. The contact is visible only
   in authenticated staff order details and is not included in public tracking.
   The pending contact is kept in that browser tab until checkout succeeds.
   The server validates current product prices,
   availability, modifier rules and GST. Client-supplied prices are never trusted.
4. The confirmation shows a pickup code, confirmed total and **Pay cash at the outlet**.
   No online payment or card details are taken.
5. Keep the confirmation tab to refresh order status. A private proof is kept in
   that tab's session storage; customers cannot enumerate all outlet orders.

Orders require an operating outlet and at least one active staff shift. Guest
submissions are rate-limited; a stable random request ID makes resubmitting the
same interrupted checkout return the existing order instead of a duplicate.

## Cash collection / kitchen

- Guest orders enter the usual order list and kitchen queue, unpaid, with a
  `[PUBLIC QR · CASH AT OUTLET]` note and the customer pickup name.
- Staff accept/prepare the kitchen ticket using the normal KDS workflow.
- Open your cash shift, find the order under **Orders & Receipts** using the pickup
  code, open the bill and **Collect cash at outlet**. Android staff can resume it
  from **Orders** and take payment through the native POS.
- The server captures only the outstanding balance, calculates change, attributes
  the cash to the collecting cashier's shift and closes the bill. Receipt and
  inventory workflows are the same as staff-created orders.

## Deployment / privacy

- `PublicBaseUrl` defaults to `https://the-bathany.onrender.com`; configure it when
  deploying to another domain so generated QR codes point to the correct site.
- The new image-table migration runs through the existing startup migration path;
  `RESET_DB_ON_START` stays false. No existing product/order tables are dropped.
- Guest pages expose only public menu/outlet data and the individual confirmation,
  never staff permissions, internal audit logs or an outlet-wide order list.
- The current free Render deployment remains a **test environment**, per the owner.
  Physical QR scans, Android gallery/print testing, reliable hosting/backups and
  abuse/capacity acceptance are still required before real go-live.
