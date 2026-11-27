"""
Seed realistic dev data for the full e-commerce stack via the API Gateway.

Usage:
    pip install httpx
    python scripts/seed-dev-data.py
    python scripts/seed-dev-data.py --gateway-url http://localhost:5133
    python scripts/seed-dev-data.py --skip-payments   # catalog + inventory + orders only

Prerequisites:
    docker compose up -d (api-gateway, auth, catalog, inventory, orders, payments, rabbitmq, ...)
    STRIPE_SECRET_KEY set in docker/.env for payment processing (sk_test_...)
    The `postgres` container running (the admin role is set directly in the auth database).

Administrative calls (products, images, attributes, relations, stock) require the Admin role.
The script creates the development administrator (DEV ONLY) the same way it creates the demo
customers: it registers the account through the public endpoint, then promotes it to Admin with
a single UPDATE inside the postgres container, because no public endpoint can grant Admin.
Re-running is safe. Override the account with --admin-email / --admin-password.
"""
from __future__ import annotations

import argparse
import asyncio
import base64
import os
import subprocess
import sys
import time
import uuid
from dataclasses import dataclass
from decimal import Decimal
from typing import Any

import httpx

DEFAULT_GATEWAY_URL = "http://localhost:5133"
ASYNC_SETTLE_SECONDS = 3

# ── Category IDs (stable GUIDs for repeatable seeds) ───────────────────────────

CATEGORY_IDS = {
    "figurines": "a1000000-0000-0000-0000-000000000001",
    "functional": "a2000000-0000-0000-0000-000000000002",
    "mechanical": "a3000000-0000-0000-0000-000000000003",
    "home_garden": "a4000000-0000-0000-0000-000000000004",
    "decor": "a5000000-0000-0000-0000-000000000005",
    "accessories": "a6000000-0000-0000-0000-000000000006",
}

# ── Users ────────────────────────────────────────────────────────────────────

USERS = [
    {
        "email": "emilia.kowalski@example.com",
        "password": "SeedShop2026!",
        "profile": "Emilia Kowalski",
    },
    {
        "email": "marco.silva@example.com",
        "password": "SeedShop2026!",
        "profile": "Marco Silva",
    },
]

# Development administrator (DEV ONLY, never use these values outside local development)
ADMIN_USER = {
    "email": "admin@store.local",
    "password": "AdminShop2026!",
}

# 1×1 PNG (transparent) — valid image for MinIO upload smoke test
TINY_PNG = base64.b64decode(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
)

# ── Product catalog (20 items) ───────────────────────────────────────────────

PRODUCTS: list[dict[str, Any]] = [
    {
        "name": "Articulated Dragon",
        "shortDescription": "Fully articulated dragon with snap-fit joints — prints in place, no supports",
        "longDescription": (
            "A stunning articulated dragon with 22 linked body segments that snap together without glue. "
            "Designed for 0.4 mm FDM nozzles. PLA for rigidity; PETG for slight flexibility. "
            "Popular desk display and gifting piece."
        ),
        "price": 24.99,
        "categoryId": CATEGORY_IDS["figurines"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 15, "nozzleSize": 4, "printTimeMinutes": 240, "filamentUsedGrams": 85.0, "supportsRequired": False},
        "size": {"widthMm": 320, "heightMm": 60, "depthMm": 80},
        "tags": ["dragon", "articulated", "print-in-place", "fantasy"],
        "supportedMaterials": ["PLA", "PETG", "ABS"],
        "compatiblePrinters": ["FDM"],
        "stock": 40,
        "attributes": [("finish", "smooth"), ("articulated", "yes"), ("beginner-friendly", "yes")],
    },
    {
        "name": "Flexi Rex Dinosaur",
        "shortDescription": "Classic flexible T-Rex print-in-place — great first calibration print",
        "longDescription": "Lattice-jointed flexi T-Rex that prints fully assembled in one session. Works in PLA or TPU.",
        "price": 14.99,
        "categoryId": CATEGORY_IDS["figurines"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 15, "nozzleSize": 4, "printTimeMinutes": 120, "filamentUsedGrams": 42.0, "supportsRequired": False},
        "size": {"widthMm": 160, "heightMm": 90, "depthMm": 55},
        "tags": ["dinosaur", "flexi", "beginner"],
        "supportedMaterials": ["PLA", "TPU", "PETG"],
        "compatiblePrinters": ["FDM"],
        "stock": 60,
        "attributes": [("articulated", "yes"), ("size", "160mm")],
    },
    {
        "name": "Modular Desk Organizer System",
        "shortDescription": "Stackable desk modules with dovetail joints — pen cup, tray, phone stand",
        "longDescription": "Mix-and-match desk organization: pen cup, deep tray, phone stand, cable channel, and mini-drawer.",
        "price": 34.99,
        "categoryId": CATEGORY_IDS["functional"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 20, "nozzleSize": 4, "printTimeMinutes": 480, "filamentUsedGrams": 210.0, "supportsRequired": False},
        "size": {"widthMm": 250, "heightMm": 100, "depthMm": 120},
        "tags": ["desk", "organizer", "modular", "office"],
        "supportedMaterials": ["PLA", "PETG"],
        "compatiblePrinters": ["FDM"],
        "stock": 35,
        "attributes": [("modular", "yes"), ("desk-friendly", "yes")],
    },
    {
        "name": "Parametric Cable Management Box",
        "shortDescription": "Snap-lid under-desk cable box with optional wall-mount slots",
        "longDescription": "Hides power bricks and cable slack. PETG recommended near warm adapters.",
        "price": 18.49,
        "categoryId": CATEGORY_IDS["functional"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 25, "nozzleSize": 4, "printTimeMinutes": 200, "filamentUsedGrams": 130.0, "supportsRequired": False},
        "size": {"widthMm": 200, "heightMm": 80, "depthMm": 100},
        "tags": ["cable", "management", "desk"],
        "supportedMaterials": ["PLA", "PETG", "ABS"],
        "compatiblePrinters": ["FDM"],
        "stock": 50,
        "attributes": [("snap-fit", "yes"), ("functional", "yes")],
    },
    {
        "name": "Planetary Gear Set",
        "shortDescription": "Print-in-place planetary gearbox — kinetic desk sculpture",
        "longDescription": "Sun, planet, and ring gears meshing after light break-in. PETG or nylon for wear resistance.",
        "price": 29.99,
        "categoryId": CATEGORY_IDS["mechanical"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.1, "infillPercentage": 40, "nozzleSize": 4, "printTimeMinutes": 350, "filamentUsedGrams": 95.0, "supportsRequired": False},
        "size": {"widthMm": 100, "heightMm": 50, "depthMm": 100},
        "tags": ["gears", "mechanical", "kinetic"],
        "supportedMaterials": ["PETG", "ABS", "Nylon"],
        "compatiblePrinters": ["FDM"],
        "stock": 25,
        "attributes": [("high-detail", "yes"), ("layer_height", "0.1mm")],
    },
    {
        "name": "Self-Watering Planter",
        "shortDescription": "Two-part passive wicking planter for succulents and herbs",
        "longDescription": "Inner pot wicks moisture from a 200 ml reservoir. Wood-fill PLA gives a natural look.",
        "price": 19.99,
        "categoryId": CATEGORY_IDS["home_garden"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 3, "nozzleSize": 4, "printTimeMinutes": 180, "filamentUsedGrams": 75.0, "supportsRequired": False},
        "size": {"widthMm": 100, "heightMm": 130, "depthMm": 100},
        "tags": ["planter", "eco-friendly", "garden"],
        "supportedMaterials": ["PLA", "PETG"],
        "compatiblePrinters": ["FDM"],
        "stock": 45,
        "attributes": [("eco-friendly", "yes"), ("finish", "matte")],
    },
    {
        "name": "Articulated Octopus",
        "shortDescription": "Eight poseable tentacles — print-in-place, no supports",
        "longDescription": "Highly detailed octopus with ball-and-socket tentacle segments. TPU for rubbery feel.",
        "price": 22.49,
        "categoryId": CATEGORY_IDS["figurines"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.15, "infillPercentage": 15, "nozzleSize": 4, "printTimeMinutes": 300, "filamentUsedGrams": 110.0, "supportsRequired": False},
        "size": {"widthMm": 200, "heightMm": 80, "depthMm": 200},
        "tags": ["octopus", "articulated", "sea"],
        "supportedMaterials": ["PLA", "TPU", "PETG"],
        "compatiblePrinters": ["FDM"],
        "stock": 38,
        "attributes": [("articulated", "yes"), ("high-detail", "yes")],
    },
    {
        "name": "Resin Miniature Knight",
        "shortDescription": "28 mm heroic-scale knight for tabletop RPG — SLA optimised",
        "longDescription": "Chainmail texture and engraved pauldrons. Hollow interior saves resin. Pre-supported STL included.",
        "price": 12.49,
        "categoryId": CATEGORY_IDS["decor"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.05, "infillPercentage": 0, "nozzleSize": 0, "printTimeMinutes": 180, "filamentUsedGrams": 12.0, "supportsRequired": True},
        "size": {"widthMm": 25, "heightMm": 50, "depthMm": 20},
        "tags": ["miniature", "knight", "28mm", "resin"],
        "supportedMaterials": ["Resin"],
        "compatiblePrinters": ["SLA"],
        "stock": 80,
        "attributes": [("miniature", "yes"), ("size", "28mm")],
    },
    {
        "name": "Magnetic Snap Lid Storage Container",
        "shortDescription": "Stackable boxes with 6×2 mm magnet pockets — four sizes",
        "longDescription": "Friction-fit lids reinforced by neodymium magnets. Ideal for screws, resistors, jewelry.",
        "price": 16.99,
        "categoryId": CATEGORY_IDS["functional"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 30, "nozzleSize": 4, "printTimeMinutes": 220, "filamentUsedGrams": 90.0, "supportsRequired": False},
        "size": {"widthMm": 80, "heightMm": 50, "depthMm": 80},
        "tags": ["storage", "magnetic", "stackable"],
        "supportedMaterials": ["PLA", "PETG"],
        "compatiblePrinters": ["FDM"],
        "stock": 55,
        "attributes": [("modular", "yes"), ("snap-fit", "yes")],
    },
    {
        "name": "Adjustable Phone and Tablet Stand",
        "shortDescription": "Minimalist stand — portrait/landscape, 65° or 75°, cable slot",
        "longDescription": "Holds devices up to 13 mm thick. Three friction-fit parts, no supports.",
        "price": 14.99,
        "categoryId": CATEGORY_IDS["accessories"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 20, "nozzleSize": 4, "printTimeMinutes": 90, "filamentUsedGrams": 35.0, "supportsRequired": False},
        "size": {"widthMm": 100, "heightMm": 110, "depthMm": 80},
        "tags": ["phone", "tablet", "stand", "desk"],
        "supportedMaterials": ["PLA", "PETG"],
        "compatiblePrinters": ["FDM"],
        "stock": 70,
        "attributes": [("desk-friendly", "yes"), ("beginner-friendly", "yes")],
    },
    {
        "name": "Voronoi Lamp Shade",
        "shortDescription": "Organic voronoi pattern lampshade for E27 bulb adapters",
        "longDescription": "Striking shadow patterns when lit. Print with translucent PETG or silk PLA.",
        "price": 27.99,
        "categoryId": CATEGORY_IDS["decor"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 0, "nozzleSize": 4, "printTimeMinutes": 420, "filamentUsedGrams": 145.0, "supportsRequired": True},
        "size": {"widthMm": 180, "heightMm": 220, "depthMm": 180},
        "tags": ["lamp", "voronoi", "decor", "lighting"],
        "supportedMaterials": ["PLA", "PETG"],
        "compatiblePrinters": ["FDM"],
        "stock": 20,
        "attributes": [("finish", "translucent"), ("decor", "yes")],
    },
    {
        "name": "Wall-Mounted Headphone Hanger",
        "shortDescription": "Screw-mount hanger with soft TPU contact pad groove",
        "longDescription": "Keeps desk clear. Includes keyhole slots for drywall anchors.",
        "price": 9.99,
        "categoryId": CATEGORY_IDS["accessories"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 25, "nozzleSize": 4, "printTimeMinutes": 45, "filamentUsedGrams": 28.0, "supportsRequired": False},
        "size": {"widthMm": 80, "heightMm": 120, "depthMm": 40},
        "tags": ["headphones", "wall-mount", "desk"],
        "supportedMaterials": ["PLA", "PETG"],
        "compatiblePrinters": ["FDM"],
        "stock": 90,
        "attributes": [("wall-mount", "yes"), ("functional", "yes")],
    },
    {
        "name": "Precision Caliper Helper Jig",
        "shortDescription": "Shop jig for repeatable caliper depth measurements on printed parts",
        "longDescription": "Machinist-friendly reference block with 10/20/30 mm steps.",
        "price": 11.49,
        "categoryId": CATEGORY_IDS["mechanical"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.12, "infillPercentage": 50, "nozzleSize": 4, "printTimeMinutes": 150, "filamentUsedGrams": 55.0, "supportsRequired": False},
        "size": {"widthMm": 120, "heightMm": 40, "depthMm": 60},
        "tags": ["tools", "calibration", "workshop"],
        "supportedMaterials": ["PETG", "ABS"],
        "compatiblePrinters": ["FDM"],
        "stock": 30,
        "attributes": [("functional", "yes"), ("precision", "yes")],
    },
    {
        "name": "Succulent Pot Trio",
        "shortDescription": "Nested set of three geometric planters — drainage holes included",
        "longDescription": "Modern faceted design. Print all three in one plate or individually.",
        "price": 17.99,
        "categoryId": CATEGORY_IDS["home_garden"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 5, "nozzleSize": 4, "printTimeMinutes": 240, "filamentUsedGrams": 95.0, "supportsRequired": False},
        "size": {"widthMm": 90, "heightMm": 70, "depthMm": 90},
        "tags": ["planter", "succulent", "set"],
        "supportedMaterials": ["PLA", "PETG"],
        "compatiblePrinters": ["FDM"],
        "stock": 42,
        "attributes": [("set-of-3", "yes"), ("drainage", "yes")],
    },
    {
        "name": "RC Car Wheel Rim 1:10",
        "shortDescription": "Lightweight 1:10 scale wheel rim — PETG for impact resistance",
        "longDescription": "12 mm hex hub compatible. Print four for a full set.",
        "price": 21.99,
        "categoryId": CATEGORY_IDS["mechanical"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.16, "infillPercentage": 35, "nozzleSize": 4, "printTimeMinutes": 280, "filamentUsedGrams": 68.0, "supportsRequired": False},
        "size": {"widthMm": 65, "heightMm": 65, "depthMm": 30},
        "tags": ["rc", "wheel", "hobby"],
        "supportedMaterials": ["PETG", "ABS", "Nylon"],
        "compatiblePrinters": ["FDM"],
        "stock": 28,
        "attributes": [("scale", "1:10"), ("hobby", "yes")],
    },
    {
        "name": "Book Nook Library Insert",
        "shortDescription": "Detailed bookshelf insert scene — LED slot for 5 mm strip",
        "longDescription": "Cozy library diorama sized for standard bookshelves. Paint-friendly matte surfaces.",
        "price": 44.99,
        "categoryId": CATEGORY_IDS["decor"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.12, "infillPercentage": 10, "nozzleSize": 4, "printTimeMinutes": 900, "filamentUsedGrams": 320.0, "supportsRequired": True},
        "size": {"widthMm": 180, "heightMm": 220, "depthMm": 120},
        "tags": ["book-nook", "diorama", "decor"],
        "supportedMaterials": ["PLA", "PETG"],
        "compatiblePrinters": ["FDM"],
        "stock": 12,
        "attributes": [("high-detail", "yes"), ("led-ready", "yes")],
    },
    {
        "name": "Filament Sample Spool Adapter",
        "shortDescription": "Universal adapter for 200 g sample spools on standard spool holders",
        "longDescription": "Snap-fit bushing fits most dry-box feed paths.",
        "price": 8.99,
        "categoryId": CATEGORY_IDS["accessories"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 40, "nozzleSize": 4, "printTimeMinutes": 60, "filamentUsedGrams": 22.0, "supportsRequired": False},
        "size": {"widthMm": 60, "heightMm": 60, "depthMm": 40},
        "tags": ["filament", "spool", "printer-accessory"],
        "supportedMaterials": ["PLA", "PETG", "ABS"],
        "compatiblePrinters": ["FDM"],
        "stock": 100,
        "attributes": [("universal", "yes"), ("snap-fit", "yes")],
    },
    {
        "name": "Mechanical Keyboard Wrist Rest",
        "shortDescription": "Ergonomic 60% keyboard wrist rest — TPU top layer recommended",
        "longDescription": "Chamfered front edge and non-slip feet pockets. PLA base + TPU pad.",
        "price": 23.99,
        "categoryId": CATEGORY_IDS["accessories"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 20, "nozzleSize": 4, "printTimeMinutes": 200, "filamentUsedGrams": 88.0, "supportsRequired": False},
        "size": {"widthMm": 300, "heightMm": 25, "depthMm": 80},
        "tags": ["keyboard", "ergonomic", "desk"],
        "supportedMaterials": ["PLA", "TPU"],
        "compatiblePrinters": ["FDM"],
        "stock": 33,
        "attributes": [("ergonomic", "yes"), ("desk-friendly", "yes")],
    },
    {
        "name": "Bird Feeder Rain Canopy",
        "shortDescription": "Snap-on rain shield for standard tube feeders",
        "longDescription": "UV-resistant PETG recommended for outdoor use.",
        "price": 15.49,
        "categoryId": CATEGORY_IDS["home_garden"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.24, "infillPercentage": 15, "nozzleSize": 4, "printTimeMinutes": 160, "filamentUsedGrams": 72.0, "supportsRequired": False},
        "size": {"widthMm": 200, "heightMm": 80, "depthMm": 200},
        "tags": ["bird", "outdoor", "garden"],
        "supportedMaterials": ["PETG", "ABS"],
        "compatiblePrinters": ["FDM"],
        "stock": 36,
        "attributes": [("outdoor", "yes"), ("weather-resistant", "recommended-petg")],
    },
    {
        "name": "Cable Chain for 3D Printer",
        "shortDescription": "10-link drag chain segment for cable management on moving gantries",
        "longDescription": "Print additional segments to extend. 4 mm inner clearance for typical harnesses.",
        "price": 13.99,
        "categoryId": CATEGORY_IDS["mechanical"],
        "currency": "EUR",
        "settings": {"layerHeight": 0.2, "infillPercentage": 30, "nozzleSize": 4, "printTimeMinutes": 110, "filamentUsedGrams": 48.0, "supportsRequired": False},
        "size": {"widthMm": 40, "heightMm": 25, "depthMm": 200},
        "tags": ["cable-chain", "printer-mod", "functional"],
        "supportedMaterials": ["PETG", "ABS", "Nylon"],
        "compatiblePrinters": ["FDM"],
        "stock": 48,
        "attributes": [("modular", "yes"), ("printer-mod", "yes")],
    },
]

RELATIONS = [
    ("Articulated Dragon", "Flexi Rex Dinosaur", "similar"),
    ("Articulated Dragon", "Articulated Octopus", "similar"),
    ("Modular Desk Organizer System", "Parametric Cable Management Box", "complementary"),
    ("Modular Desk Organizer System", "Adjustable Phone and Tablet Stand", "complementary"),
    ("Adjustable Phone and Tablet Stand", "Wall-Mounted Headphone Hanger", "complementary"),
    ("Planetary Gear Set", "Precision Caliper Helper Jig", "similar"),
    ("Resin Miniature Knight", "Book Nook Library Insert", "similar"),
    ("Self-Watering Planter", "Succulent Pot Trio", "similar"),
]

# ── Data classes ─────────────────────────────────────────────────────────────


@dataclass
class AuthSession:
    user_id: str
    email: str
    access_token: str


@dataclass
class SeededProduct:
    product_id: str
    name: str
    price: Decimal
    currency: str
    image_url: str | None


# ── HTTP helpers ─────────────────────────────────────────────────────────────


def correlation_headers() -> dict[str, str]:
    return {"X-Correlation-Id": str(uuid.uuid4())}


async def ensure_user(client: httpx.AsyncClient, gateway: str, email: str, password: str) -> AuthSession:
    login = await client.post(
        f"{gateway}/auth/login",
        json={"email": email, "password": password},
        headers=correlation_headers(),
    )
    if login.status_code == 200:
        body = login.json()
        token = body["accessToken"]
        user_id = _user_id_from_token(token)
        print(f"  ✓ Logged in {email} → {user_id}")
        return AuthSession(user_id=user_id, email=email, access_token=token)

    register = await client.post(
        f"{gateway}/auth/register",
        json={"email": email, "password": password},
        headers=correlation_headers(),
    )
    if register.status_code not in (200, 201):
        raise RuntimeError(f"Register failed for {email}: {register.status_code} {register.text}")

    user_id = register.json()
    if isinstance(user_id, dict):
        user_id = user_id.get("id") or user_id.get("userId")

    login = await client.post(
        f"{gateway}/auth/login",
        json={"email": email, "password": password},
        headers=correlation_headers(),
    )
    login.raise_for_status()
    token = login.json()["accessToken"]
    print(f"  ✓ Registered {email} → {user_id}")
    return AuthSession(user_id=str(user_id), email=email, access_token=token)


def _user_id_from_token(token: str) -> str:
    """Extract sub/nameidentifier claim from JWT without external deps."""
    import json

    payload_b64 = token.split(".")[1]
    padding = "=" * (-len(payload_b64) % 4)
    payload = json.loads(base64.urlsafe_b64decode(payload_b64 + padding))
    for key in ("sub", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"):
        if key in payload:
            return str(payload[key])
    raise ValueError("Could not read user id from access token")


def auth_headers(session: AuthSession) -> dict[str, str]:
    return {**correlation_headers(), "Authorization": f"Bearer {session.access_token}"}


def promote_to_admin(email: str, container: str, db_user: str, db_name: str) -> None:
    """Set the account's role to Admin directly in the auth database (dev only).

    The public API cannot create administrators, so the dev admin is promoted with one UPDATE.
    """
    normalized = email.strip().lower().replace("'", "''")
    sql = f"UPDATE \"Users\" SET \"Role\" = 'Admin' WHERE \"NormalizedEmail\" = '{normalized}';"
    try:
        result = subprocess.run(
            ["docker", "exec", container, "psql", "-U", db_user, "-d", db_name, "-v", "ON_ERROR_STOP=1", "-c", sql],
            capture_output=True,
            text=True,
            check=False,
        )
    except FileNotFoundError:
        print("✗ The docker CLI was not found; it is needed to promote the dev admin.")
        sys.exit(1)
    if result.returncode != 0 or "UPDATE 1" not in result.stdout:
        print(f"✗ Could not promote {email} to Admin in container '{container}' (database '{db_name}').")
        print(f"  {(result.stderr or result.stdout).strip()}")
        print("  Check --postgres-container / --postgres-user / --postgres-db match docker/.env.")
        sys.exit(1)


async def ensure_admin(
    client: httpx.AsyncClient,
    gateway: str,
    email: str,
    password: str,
    container: str,
    db_user: str,
    db_name: str,
) -> AuthSession:
    """Create the dev admin if needed, promote it to Admin, and return a session carrying the Admin role."""
    await ensure_user(client, gateway, email, password)
    promote_to_admin(email, container, db_user, db_name)

    # Sign in again: the role is part of the access token, so the earlier token is stale.
    login = await client.post(
        f"{gateway}/auth/login",
        json={"email": email, "password": password},
        headers=correlation_headers(),
    )
    login.raise_for_status()
    token = login.json()["accessToken"]
    user_id = _user_id_from_token(token)
    print(f"  ✓ Admin ready: {email} → {user_id}")
    return AuthSession(user_id=user_id, email=email, access_token=token)

async def create_product(
    client: httpx.AsyncClient, gateway: str, product: dict[str, Any], admin: AuthSession
) -> str:
    payload = {k: v for k, v in product.items() if k not in ("attributes", "stock")}
    resp = await client.post(f"{gateway}/products", json=payload, headers=auth_headers(admin))
    if resp.status_code == 400 and "already exists" in resp.text:
        # Product names are unique, so a re-run finds the product from the previous run.
        existing = await find_product(client, gateway, product["name"], admin)
        if existing is not None:
            print(f"  = Product '{product['name']}' already exists → {existing['id']}")
            return existing["id"]
    if resp.status_code not in (200, 201):
        raise RuntimeError(f"Create product '{product['name']}' failed: {resp.status_code} {resp.text}")
    product_id = resp.json()["productId"]
    print(f"  ✓ Product '{product['name']}' → {product_id}")
    return product_id


async def find_product(
    client: httpx.AsyncClient, gateway: str, name: str, admin: AuthSession
) -> dict[str, Any] | None:
    """Looks a product up by exact name across every status (admin list)."""
    resp = await client.get(
        f"{gateway}/products/admin",
        params={"q": name, "pageSize": 100},
        headers=auth_headers(admin),
    )
    if resp.status_code != 200:
        return None
    return next((p for p in resp.json().get("items", []) if p["name"].lower() == name.lower()), None)


async def publish_product(
    client: httpx.AsyncClient, gateway: str, product_id: str, product_name: str, admin: AuthSession
) -> None:
    """Products are created as drafts and invisible to shoppers until published."""
    resp = await client.post(
        f"{gateway}/products/{product_id}/status",
        json={"status": "Active"},
        headers=auth_headers(admin),
    )
    if resp.status_code != 200:
        raise RuntimeError(f"Publish '{product_name}' failed: {resp.status_code} {resp.text}")


async def upload_placeholder_image(
    client: httpx.AsyncClient, gateway: str, product_id: str, product_name: str, admin: AuthSession
) -> str | None:
    current = await client.get(f"{gateway}/products/{product_id}", headers=auth_headers(admin))
    if current.status_code == 200 and current.json().get("images"):
        return current.json()["images"][0]["url"]  # already has an image from a previous run

    files = {"file": (f"{product_name[:30].replace(' ', '-')}.png", TINY_PNG, "image/png")}
    data = {"isPrimary": "true"}
    resp = await client.post(
        f"{gateway}/products/{product_id}/images",
        files=files,
        data=data,
        headers=auth_headers(admin),
    )
    if resp.status_code not in (200, 201):
        print(f"    ⚠ Image upload skipped for {product_name}: {resp.status_code}")
        return None
    url = resp.json().get("url")
    print(f"    + image uploaded")
    return url


async def adjust_inventory(
    client: httpx.AsyncClient, gateway: str, product_id: str, delta: int, admin: AuthSession
) -> None:
    resp = await client.post(
        f"{gateway}/inventory/{product_id}/adjustments",
        json={"delta": delta, "reason": "Initial warehouse stock for dev seed"},
        headers=auth_headers(admin),
    )
    if resp.status_code != 200:
        raise RuntimeError(f"Inventory adjust failed for {product_id}: {resp.status_code} {resp.text}")


async def create_order(
    client: httpx.AsyncClient,
    gateway: str,
    session: AuthSession,
    items: list[dict[str, Any]],
    shipping: dict[str, str],
    notes: str | None,
) -> tuple[str, Decimal]:
    total = sum(Decimal(str(i["unitPrice"])) * i["quantity"] for i in items)
    payload = {
        "userId": session.user_id,
        "currency": items[0]["currency"],
        "notes": notes,
        "recipientEmail": session.email,
        "shippingAddress": shipping,
        "items": items,
    }
    resp = await client.post(f"{gateway}/orders", json=payload, headers=auth_headers(session))
    if resp.status_code not in (200, 201):
        raise RuntimeError(f"Create order failed: {resp.status_code} {resp.text}")
    order_id = resp.json()["orderId"]
    return order_id, total


async def create_payment(
    client: httpx.AsyncClient,
    gateway: str,
    session: AuthSession,
    order_id: str,
    amount: Decimal,
    currency: str,
    idempotency_key: str,
    payment_token: str,
) -> str:
    payload = {
        "orderId": order_id,
        "userId": session.user_id,
        "amount": float(amount),
        "currency": currency,
        "method": "CreditCard",
        "idempotencyKey": idempotency_key,
        "paymentToken": payment_token,
        "recipientEmail": session.email,
    }
    resp = await client.post(f"{gateway}/payments", json=payload, headers=auth_headers(session))
    if resp.status_code not in (200, 201):
        raise RuntimeError(f"Create payment failed: {resp.status_code} {resp.text}")
    body = resp.json()
    return body.get("paymentId") or body.get("id")


async def process_payment(
    client: httpx.AsyncClient, gateway: str, session: AuthSession, payment_id: str
) -> bool:
    resp = await client.post(
        f"{gateway}/payments/{payment_id}/process",
        headers=auth_headers(session),
    )
    return resp.status_code == 200


# ── Main seed orchestration ──────────────────────────────────────────────────


async def seed(
    gateway_url: str,
    skip_payments: bool,
    admin_email: str,
    admin_password: str,
    pg_container: str,
    pg_user: str,
    pg_db: str,
) -> None:
    print(f"\n{'=' * 60}")
    print(f"Seeding dev data via API Gateway: {gateway_url}")
    print(f"{'=' * 60}\n")

    created: dict[str, SeededProduct] = {}

    async with httpx.AsyncClient(timeout=60.0) as client:
        # Health probe
        try:
            await client.get(f"{gateway_url}/products?page=1&pageSize=1")
        except httpx.ConnectError as exc:
            print(f"✗ Cannot reach gateway at {gateway_url}: {exc}")
            sys.exit(1)

        # ── 1. Users ───────────────────────────────────────────────────
        print("── 1. Auth users ───────────────────────────────────────────")
        sessions: dict[str, AuthSession] = {}
        for user in USERS:
            session = await ensure_user(client, gateway_url, user["email"], user["password"])
            sessions[user["email"]] = session

        emilia = sessions["emilia.kowalski@example.com"]
        marco = sessions["marco.silva@example.com"]

        # Administrative operations need an Admin (dev only; promoted directly in the auth database).
        admin = await ensure_admin(client, gateway_url, admin_email, admin_password, pg_container, pg_user, pg_db)

        # ── 2. Products ──────────────────────────────────────────────────
        print("\n── 2. Catalog products (20) ────────────────────────────────")
        for product in PRODUCTS:
            product_id = await create_product(client, gateway_url, product, admin)
            image_url = await upload_placeholder_image(client, gateway_url, product_id, product["name"], admin)
            await publish_product(client, gateway_url, product_id, product["name"], admin)
            created[product["name"]] = SeededProduct(
                product_id=product_id,
                name=product["name"],
                price=Decimal(str(product["price"])),
                currency=product["currency"],
                image_url=image_url,
            )

        # ── 3. Attributes ──────────────────────────────────────────────
        print("\n── 3. Product attributes ───────────────────────────────────")
        for product in PRODUCTS:
            seeded = created[product["name"]]
            for key, value in product.get("attributes", []):
                resp = await client.post(
                    f"{gateway_url}/products/{seeded.product_id}/attributes",
                    json={"key": key, "value": value},
                    headers=auth_headers(admin),
                )
                if resp.status_code not in (200, 201):
                    print(f"    ⚠ attr {key} on {product['name']}: {resp.status_code}")

        # ── 4. Relations ───────────────────────────────────────────────
        print("\n── 4. Product relations ────────────────────────────────────")
        for source, target, rel_type in RELATIONS:
            src = created.get(source)
            tgt = created.get(target)
            if not src or not tgt:
                continue
            await client.post(
                f"{gateway_url}/products/{src.product_id}/relations",
                json={"relatedProductId": tgt.product_id, "type": rel_type},
                headers=auth_headers(admin),
            )

        # ── 5. Inventory ───────────────────────────────────────────────
        print("\n── 5. Inventory stock ──────────────────────────────────────")
        for product in PRODUCTS:
            seeded = created[product["name"]]
            await adjust_inventory(client, gateway_url, seeded.product_id, product["stock"], admin)
            print(f"  ✓ {product['name']}: +{product['stock']} on hand")

        print(f"\n  ⏳ Waiting {ASYNC_SETTLE_SECONDS}s for GraphRAG ingestion...")
        await asyncio.sleep(ASYNC_SETTLE_SECONDS)

        # ── 6. Orders ──────────────────────────────────────────────────
        print("\n── 6. Orders (5) ───────────────────────────────────────────")

        def item(name: str, qty: int = 1) -> dict[str, Any]:
            p = created[name]
            return {
                "productId": p.product_id,
                "productName": p.name,
                "unitPrice": float(p.price),
                "quantity": qty,
                "currency": p.currency,
                "productImageUrl": p.image_url,
            }

        emilia_address = {
            "street": "ul. Marszałkowska 84/92",
            "city": "Warsaw",
            "state": "Mazovia",
            "country": "PL",
            "zipCode": "00-514",
            "fullName": "Emilia Kowalski",
            "phone": "+48 22 123 4567",
        }
        marco_address = {
            "street": "Rua Augusta 127",
            "city": "Lisbon",
            "state": "Lisbon",
            "country": "PT",
            "zipCode": "1100-053",
            "fullName": "Marco Silva",
            "phone": "+351 21 987 6543",
        }

        order_specs = [
            {
                "label": "Emilia — dragon + organizer (will pay)",
                "session": emilia,
                "items": [item("Articulated Dragon"), item("Modular Desk Organizer System")],
                "address": emilia_address,
                "notes": "Please pack figurines with extra bubble wrap.",
                "payment": "success",
            },
            {
                "label": "Emilia — phone stand (will pay)",
                "session": emilia,
                "items": [item("Adjustable Phone and Tablet Stand", 2)],
                "address": emilia_address,
                "notes": None,
                "payment": "success",
            },
            {
                "label": "Marco — figurine bundle (will pay)",
                "session": marco,
                "items": [
                    item("Flexi Rex Dinosaur"),
                    item("Articulated Octopus"),
                    item("Self-Watering Planter"),
                ],
                "address": marco_address,
                "notes": "Gift order — no price tags in box.",
                "payment": "success",
            },
            {
                "label": "Marco — gear set (payment pending)",
                "session": marco,
                "items": [item("Planetary Gear Set")],
                "address": marco_address,
                "notes": "Will complete payment tonight.",
                "payment": "pending",
            },
            {
                "label": "Emilia — cable box (payment will fail)",
                "session": emilia,
                "items": [item("Parametric Cable Management Box")],
                "address": emilia_address,
                "notes": "Testing declined card flow.",
                "payment": "decline",
            },
        ]

        orders: list[dict[str, Any]] = []
        for spec in order_specs:
            order_id, total = await create_order(
                client,
                gateway_url,
                spec["session"],
                spec["items"],
                spec["address"],
                spec["notes"],
            )
            orders.append({**spec, "orderId": order_id, "total": total})
            print(f"  ✓ {spec['label']} → {order_id} (€{total:.2f})")

        print(f"\n  ⏳ Waiting {ASYNC_SETTLE_SECONDS}s for inventory reservations...")
        await asyncio.sleep(ASYNC_SETTLE_SECONDS)

        # ── 7. Payments ────────────────────────────────────────────────
        if skip_payments:
            print("\n── 7. Payments SKIPPED (--skip-payments) ───────────────────")
        else:
            print("\n── 7. Payments (5) ─────────────────────────────────────────")
            print("  Requires STRIPE_SECRET_KEY in docker/.env (sk_test_...)")
            for i, order in enumerate(orders, start=1):
                token = "pm_card_visa"
                if order["payment"] == "decline":
                    token = "pm_card_chargeDeclined"

                payment_id = await create_payment(
                    client,
                    gateway_url,
                    order["session"],
                    order["orderId"],
                    order["total"],
                    order["items"][0]["currency"],
                    f"seed-order-{order['orderId']}-attempt-1",
                    token,
                )
                print(f"  ✓ Payment created for {order['label']} → {payment_id}")

                if order["payment"] == "pending":
                    print("    → left Pending (not processed)")
                    continue

                ok = await process_payment(client, gateway_url, order["session"], payment_id)
                status = "Completed" if ok and order["payment"] == "success" else "Failed/Declined"
                print(f"    → process: {status}")

            print(f"\n  ⏳ Waiting {ASYNC_SETTLE_SECONDS}s for payment.completed side effects...")
            print("     (orders→Paid, inventory deduct, PDF invoices, notifications)")
            await asyncio.sleep(ASYNC_SETTLE_SECONDS)

    # ── Summary ────────────────────────────────────────────────────────
    print(f"\n{'=' * 60}")
    print("✓ Seed complete")
    print(f"{'=' * 60}")
    print(f"  Users:     {len(USERS)}")
    print(f"  Products:  {len(created)}")
    print(f"  Orders:    {len(orders)}")
    print(f"  Payments:  {0 if skip_payments else len(orders)}")
    print()
    print("Customer login credentials:")
    for user in USERS:
        print(f"  • {user['email']} / {user['password']}")
    print()
    print("Admin login credentials (DEV ONLY):")
    print(f"  • {admin_email} / {admin_password}")
    print()
    print("Storefront: http://localhost:3000")
    print(f"Gateway:    {gateway_url}")
    print("MailHog:    http://localhost:8025  (payment notification emails)")
    print("GraphRAG:   POST {}/graphrag/api/search".format(gateway_url))
    print()


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed dev data via API Gateway")
    parser.add_argument("--gateway-url", default=DEFAULT_GATEWAY_URL)
    parser.add_argument("--skip-payments", action="store_true")
    parser.add_argument("--admin-email", default=ADMIN_USER["email"])
    parser.add_argument("--admin-password", default=ADMIN_USER["password"])
    parser.add_argument("--postgres-container", default="postgres", help="Docker container running Postgres")
    parser.add_argument("--postgres-user", default=os.environ.get("POSTGRES_USER", "postgres"))
    parser.add_argument("--postgres-db", default=os.environ.get("POSTGRES_DB", "ecommerce"), help="Database used by the auth service")
    args = parser.parse_args()
    asyncio.run(
        seed(
            args.gateway_url,
            args.skip_payments,
            args.admin_email,
            args.admin_password,
            args.postgres_container,
            args.postgres_user,
            args.postgres_db,
        )
    )

if __name__ == "__main__":
    main()
