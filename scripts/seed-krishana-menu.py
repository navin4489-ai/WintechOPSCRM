"""Seed the full food + sweets menu for Krishana Outlet (outlet 2).

Creates the categories, the items with INR prices, a generated branded tile per
item, and uploads each tile to the product-image endpoint (stored in
PostgreSQL and served on the POS, catalogue and public QR menu).

Idempotent: it reads the outlet first and skips anything that already exists,
so it is safe to re-run after adding or editing items in the app.
"""
from __future__ import annotations

import colorsys
import io
import json
import os
import sys
import urllib.error
import urllib.request
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from tilegen import tile  # noqa: E402

BASE = "https://the-bathany.onrender.com/api"
OUTLET = 2

# (category name, sort order, sku prefix, (top rgb), (bottom rgb), [items])
# item = (name, price in INR, description)
MENU: list[tuple[str, int, str, tuple, tuple, list[tuple]]] = [
    ("Starters (Veg)", 10, "STR", (46, 125, 50), (24, 74, 28), [
        ("Paneer Tikka", 280, "Chargrilled cottage cheese in a spiced yogurt marinade."),
        ("Veg Seekh Kebab", 240, "Grilled minced vegetables with herbs and spices."),
        ("Hara Bhara Kabab", 220, "Spinach and green pea patties, crisp outside."),
        ("Aloo Tikki Chaat", 150, "Crisp potato patties with chutneys and yogurt."),
        ("Veg Spring Roll", 180, "Crisp rolls stuffed with shredded vegetables."),
        ("Crispy Corn Chilli", 240, "Sweet corn tossed with chilli and curry leaf."),
        ("Dahi Ke Kebab", 260, "Hung curd and cashew kebabs, soft centered."),
        ("Masala Papad", 60, "Roasted papad topped with onion and tomato."),
        ("French Fries", 120, "Salted potato fries."),
        ("Paneer 65", 300, "Deep-fried cottage cheese in a tangy chilli sauce."),
    ]),
    ("Starters (Non-Veg)", 20, "STN", (198, 40, 40), (136, 14, 14), [
        ("Chicken Tikka", 350, "Charcoal-grilled chicken in a yogurt marinade."),
        ("Tandoori Chicken (Half)", 420, "Whole half chicken roasted in the tandoor."),
        ("Chicken Seekh Kebab", 380, "Minced chicken kebabs with green herbs."),
        ("Chicken 65", 340, "Crisp fried chicken with chilli and curry leaf."),
        ("Fish Fry", 400, "Rava-coated fish fillets, pan fried."),
        ("Egg Pepper Fry", 220, "Boiled eggs tossed in cracked pepper masala."),
        ("Prawns Fry", 520, "Masala prawns, shallow fried."),
        ("Chicken Lollipop", 360, "Frenched drumettes in a schezwan glaze."),
    ]),
    # The outlet already ships two paneer dishes in its "main course" category;
    # they are kept there and given images rather than duplicated.
    ("Main Course (Veg)", 30, "MCV", (0, 105, 92), (0, 66, 58), [
        ("Paneer Butter Masala", 340, "Cottage cheese in a silky tomato-butter gravy."),
        ("Kadhai Paneer", 340, "Cottage cheese with peppers in a coriander masala."),
        ("Malai Kofta", 320, "Paneer-potato dumplings in a creamy white gravy."),
        ("Matar Paneer", 300, "Cottage cheese and peas in onion-tomato gravy."),
        ("Dal Makhani", 260, "Black lentils simmered overnight with butter."),
        ("Dal Fry", 180, "Yellow lentils tempered with garlic and cumin."),
        ("Chana Masala", 220, "Chickpeas in a tangy Punjabi masala."),
        ("Rajma Masala", 230, "Red kidney beans in a rich gravy."),
        ("Aloo Gobi", 220, "Potiflower and cauliflower dry curry."),
        ("Mix Veg", 240, "Seasonal vegetables in a light masala."),
        ("Veg Kofta", 300, "Vegetable dumplings in a cashew gravy."),
        ("Kadhai Mushroom", 280, "Button mushrooms with peppers and coriander."),
    ]),
    ("Main Course (Non-Veg)", 40, "MCN", (123, 31, 62), (74, 14, 38), [
        ("Butter Chicken", 420, "Tandoori chicken in a mild tomato-butter gravy."),
        ("Chicken Curry", 380, "Home-style chicken curry with bone."),
        ("Kadhai Chicken", 400, "Chicken with peppers in a coriander masala."),
        ("Chicken Tikka Masala", 420, "Charred chicken tikka in a spiced gravy."),
        ("Chicken Handi", 430, "Slow-cooked chicken in a sealed pot masala."),
        ("Egg Curry", 200, "Boiled eggs in an onion-tomato gravy."),
        ("Mutton Curry", 600, "Bone-in mutton, slow cooked."),
        ("Mutton Rogan Josh", 650, "Kashmiri mutton with deep red chilli."),
        ("Fish Curry", 420, "Fish simmered in a coconut-tamarind curry."),
        ("Prawns Masala", 550, "Prawns in a coastal style masala."),
    ]),
    ("Breads", 50, "BRD", (191, 105, 0), (140, 71, 0), [
        ("Tandoori Roti", 40, "Whole wheat roti from the tandoor."),
        ("Butter Tandoori Roti", 50, "Tandoori roti finished with butter."),
        ("Plain Naan", 60, "Soft leavened white flour bread."),
        ("Butter Naan", 70, "Naan finished with butter."),
        ("Garlic Naan", 85, "Naan topped with garlic and coriander."),
        ("Cheese Naan", 110, "Naan stuffed with melting cheese."),
        ("Laccha Paratha", 75, "Layered whole wheat paratha."),
        ("Missi Roti", 60, "Gram flour roti with onion and spices."),
        ("Stuffed Kulcha", 95, "Kulcha stuffed with spiced potato."),
        ("Peshwari Naan", 100, "Sweet naan stuffed with dry fruit."),
    ]),
    ("Rice & Biryani", 60, "RIC", (245, 166, 35), (191, 108, 8), [
        ("Steamed Rice", 140, "Basmati rice, steamed."),
        ("Jeera Rice", 170, "Basmati rice tempered with cumin."),
        ("Veg Biryani", 260, "Dum-cooked basmati with vegetables."),
        ("Chicken Biryani", 340, "Dum-cooked basmati with chicken."),
        ("Mutton Biryani", 550, "Dum-cooked basmati with mutton."),
        ("Fish Biryani", 480, "Dum-cooked basmati with fish."),
        ("Veg Pulao", 220, "Basmati pulao with vegetables."),
        ("Curd Rice", 150, "Rice folded through with yogurt."),
        ("Lemon Rice", 160, "Tempered rice with lemon and peanut."),
    ]),
    ("Sweets (Mithai)", 70, "SWT", (186, 104, 200), (123, 31, 161), [
        ("Gulab Jamun (2 pcs)", 90, "Warm milk dumplings in cardamom syrup."),
        ("Rasmalai (2 pcs)", 130, "Soft cheese discs in saffron milk."),
        ("Rasgulla (2 pcs)", 100, "Spongy syrup balls."),
        ("Balushahi (2 pcs)", 110, "Flaky pastry soaked in syrup."),
        ("Soan Papdi", 180, "Flaky gram flour sweet, 250 g."),
        ("Kaju Katli", 450, "Cashew diamond slices, 250 g."),
        ("Motichoor Ladoo", 280, "Gram flour boondi ladoos, 250 g."),
        ("Jalebi", 240, "Fresh spirals fried and soaked in syrup, 250 g."),
        ("Milk Cake", 320, "Caramelized milk cake, 250 g."),
        ("Peda", 300, "Khoya peda with cardamom, 250 g."),
        ("Dry Fruit Barfi", 480, "Assorted dry fruit barfi, 250 g."),
        ("Bengali Sandesh", 150, "Fresh chhena sweet."),
        ("Gajar Halwa", 200, "Warm carrot pudding with khoya."),
        ("Moong Dal Halwa", 260, "Slow-roasted moong dal halwa."),
        ("Kheer", 140, "Rice pudding with dry fruit."),
        ("Rabri", 180, "Thickened sweetened milk."),
    ]),
    ("Beverages", 80, "BEV", (21, 101, 192), (13, 66, 144), [
        ("Masala Chai", 40, "Tea brewed with ginger and cardamom."),
        ("Filter Coffee", 50, "South Indian style filter coffee."),
        ("Cold Coffee", 130, "Blended chilled coffee."),
        ("Sweet Lassi", 110, "Churned yogurt drink."),
        ("Salted Lassi", 100, "Savory yogurt drink with roasted cumin."),
        ("Mango Lassi", 140, "Yogurt drink blended with mango."),
        ("Fresh Lime Soda", 90, "Sweet, salted or mixed."),
        ("Sweet Lime Juice", 100, "Freshly pressed mosambi juice."),
        ("Buttermilk", 60, "Spiced diluted yogurt."),
        ("Mineral Water", 20, "Sealed bottle, 500 ml."),
        ("Soft Drink", 40, "Chilled bottled soft drink."),
    ]),
    ("Soups & Salads", 90, "SOU", (85, 139, 47), (46, 95, 26), [
        ("Tomato Soup", 150, "Cream of tomato with herbs."),
        ("Sweet Corn Soup", 160, "Vegetable sweet corn soup."),
        ("Hot & Sour Soup", 170, "Spicy and tangy vegetable soup."),
        ("Veg Manchow Soup", 170, "Thick Indo-Chinese soup with fried noodles."),
        ("Green Salad", 130, "Cucumber, tomato and onion."),
        ("Onion Salad", 70, "Sliced onion with lemon."),
        ("Raita", 90, "Yogurt with cucumber and cumin."),
    ]),
]


def api(method: str, path: str, token: str, payload=None, raw=None, ctype=None):
    """One JSON-or-raw call against the API, returning (status, parsed)."""
    url = f"{BASE}{path}"
    headers = {"Authorization": f"Bearer {token}"}
    data = None
    if raw is not None:
        data = raw
        headers["Content-Type"] = ctype
    elif payload is not None:
        data = json.dumps(payload).encode()
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=90) as resp:
            return resp.status, json.loads(resp.read().decode())
    except urllib.error.HTTPError as exc:
        body = exc.read().decode(errors="replace")
        try:
            return exc.code, json.loads(body)
        except json.JSONDecodeError:
            return exc.code, {"errors": [body[:200]]}


def multipart(fields: dict, files: dict) -> tuple[bytes, str]:
    """Build a multipart/form-data body from text fields and binary files."""
    boundary = uuid.uuid4().hex
    out = io.BytesIO()
    for name, value in fields.items():
        out.write(f"--{boundary}\r\n".encode())
        out.write(f'Content-Disposition: form-data; name="{name}"\r\n\r\n'.encode())
        out.write(f"{value}\r\n".encode())
    for name, (filename, blob, kind) in files.items():
        out.write(f"--{boundary}\r\n".encode())
        out.write(
            f'Content-Disposition: form-data; name="{name}"; filename="{filename}"\r\n'.encode()
        )
        out.write(f"Content-Type: {kind}\r\n\r\n".encode())
        out.write(blob)
        out.write(b"\r\n")
    out.write(f"--{boundary}--\r\n".encode())
    return out.getvalue(), f"multipart/form-data; boundary={boundary}"


def login() -> str:
    status, body = api("POST", "/auth/login", "", {"username": "admin", "password": "Admin@123"})
    if status != 200 or not body.get("success"):
        raise SystemExit(f"login failed: {body}")
    return body["data"]["token"]


def tile_jpeg(name: str, top: tuple, bottom: tuple, seed: int, price: float) -> bytes:
    """A JPEG tile, hue-shifted a little per item so neighbours differ."""
    shift = ((seed * 37) % 13 - 6) / 100.0
    def nudge(rgb):
        h, l, s = colorsys.rgb_to_hls(*(c / 255 for c in rgb))
        h = (h + shift) % 1.0
        return tuple(round(c * 255) for c in colorsys.hls_to_rgb(h, l, s))
    img = tile(name, nudge(top), nudge(bottom), price)
    buf = io.BytesIO()
    img.save(buf, "JPEG", quality=86, optimize=True)
    return buf.getvalue()


def main() -> None:
    # --force-images regenerates and re-uploads every tile (used when the tile
    # design changes); without it, items that already have an image are skipped.
    force = "--force-images" in sys.argv
    token = login()

    _, body = api("GET", f"/categories/outlet/{OUTLET}", token)
    categories = {c["name"].lower(): c for c in body["data"]}

    _, body = api("GET", f"/products?outletId={OUTLET}&availableOnly=false", token)
    # The API serialises camelCase (sku, name, imageUrl), not the C# property names.
    by_sku = {p["sku"]: p for p in body["data"]}
    by_name = {p["name"].lower(): p for p in body["data"]}
    print(f"existing: {len(categories)} categories, {len(by_sku)} products")

    created_cats = created_items = with_images = 0
    uploaded: set[int] = set()
    seq = 0
    for cat_name, order, prefix, top, bottom, items in MENU:
        key = cat_name.lower()
        if key not in categories:
            status, body = api("POST", "/categories", token,
                               {"OutletId": OUTLET, "Name": cat_name, "SortOrder": order})
            if status != 201 or not body.get("success"):
                print(f"  ! category '{cat_name}' failed: {body.get('errors')}")
                continue
            categories[key] = body["data"]
            created_cats += 1
        cat_id = categories[key]["id"]

        for idx, (name, price, description) in enumerate(items, start=1):
            seq += 1
            sku = f"KRI-{prefix}-{idx:03d}"
            existing = by_sku.get(sku) or by_name.get(name.lower())
            if existing is None:
                status, body = api("POST", "/products", token, {
                    "OutletId": OUTLET, "CategoryId": cat_id, "SKU": sku,
                    "Name": name, "Description": description,
                    "Price": price, "TaxClass": "Standard",
                    "IsAvailable": True, "SortOrder": idx * 10,
                })
                if status != 201 or not body.get("success"):
                    print(f"  ! item '{name}' failed: {body.get('errors')}")
                    continue
                existing = body["data"]
                by_sku[sku] = existing
                created_items += 1

            if existing.get("imageUrl") and not force:
                with_images += 1
                continue

            blob = tile_jpeg(name, top, bottom, seq, price)
            body_bytes, ctype = multipart({}, {"file": (f"{sku}.jpg", blob, "image/jpeg")})
            status, body = api("POST", f"/products/{existing['id']}/image", token,
                               raw=body_bytes, ctype=ctype)
            if status == 200 and body.get("success"):
                with_images += 1
                uploaded.add(existing["id"])
            else:
                print(f"  ! image for '{name}' failed: {body.get('errors')}")

    _, body = api("GET", f"/products?outletId={OUTLET}&availableOnly=false", token)
    products = body["data"]
    missing = [p["name"] for p in products if not p.get("imageUrl")]

    # Anything already on the outlet's books (the two paneer dishes that ship
    # in its "main course" category) still needs a tile if it has none.
    mcv_top, mcv_bottom = (0, 105, 92), (0, 66, 58)
    for p in products:
        if p["id"] in uploaded or (p.get("imageUrl") and not force):
            continue
        blob = tile_jpeg(p["name"], mcv_top, mcv_bottom, p["id"], p["price"])
        body_bytes, ctype = multipart({}, {"file": (f"{p['sku']}.jpg", blob, "image/jpeg")})
        status, body = api("POST", f"/products/{p['id']}/image", token,
                           raw=body_bytes, ctype=ctype)
        if status == 200 and body.get("success"):
            with_images += 1
            if p["name"] in missing:
                missing.remove(p["name"])
        else:
            print(f"  ! image for existing '{p['name']}' failed: {body.get('errors')}")

    # Report from a fresh read so the tally is what the API actually serves.
    _, body = api("GET", f"/products?outletId={OUTLET}&availableOnly=false", token)
    products = body["data"]
    covered = [p for p in products if p.get("imageUrl")]
    missing = [p["name"] for p in products if not p.get("imageUrl")]

    print(f"\ncategories created: {created_cats}")
    print(f"items created:      {created_items}")
    print(f"images now:         {len(covered)}/{len(products)}")
    print(f"total products:     {len(products)}")
    if missing:
        print(f"missing images ({len(missing)}): {', '.join(missing)}")


if __name__ == "__main__":
    main()
