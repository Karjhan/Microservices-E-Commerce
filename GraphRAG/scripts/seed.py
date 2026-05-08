"""
python -m scripts.seed
"""
import asyncio
import argparse
import sys
import httpx

# ──────────────────────────────────────────────
# Configuration
# ──────────────────────────────────────────────

DEFAULT_GATEWAY_URL = "http://localhost:5133"

CATEGORY_IDS = {
    "figurines":   "a1000000-0000-0000-0000-000000000001",
    "functional":  "a2000000-0000-0000-0000-000000000002",
    "mechanical":  "a3000000-0000-0000-0000-000000000003",
    "home_garden": "a4000000-0000-0000-0000-000000000004",
    "decor":       "a5000000-0000-0000-0000-000000000005",
    "accessories": "a6000000-0000-0000-0000-000000000006",
}

# ──────────────────────────────────────────────
# Product definitions
# ──────────────────────────────────────────────

PRODUCTS = [
    {
        "Name": "Articulated Dragon",
        "ShortDescription": "Fully articulated dragon with snap-fit joints, prints in place without supports",
        "LongDescription": (
            "A stunning articulated dragon model featuring 22 individually linked body segments "
            "that snap together without any glue or hardware. Designed for FDM printers with a "
            "0.4 mm nozzle. The snap-fit joints allow full posability while keeping the print "
            "clean and support-free. PLA recommended for rigidity; PETG for a slightly more "
            "flexible result. Ideal desk decoration or display piece."
        ),
        "Price": 4.99,
        "CategoryId": CATEGORY_IDS["figurines"],
        "Currency": "USD",
        "Settings": {
            "LayerHeight": 0.2,
            "InfillPercentage": 15,
            "NozzleSize": 4,
            "PrintTimeMinutes": 240,
            "FilamentUsedGrams": 85.0,
            "SupportsRequired": False,
        },
        "Size": {"WidthMm": 320, "HeightMm": 60, "DepthMm": 80},
        "Tags": ["dragon", "articulated", "print-in-place", "no-supports", "desk-toy", "fantasy"],
        "SupportedMaterials": ["PLA", "PETG", "ABS"],
        "CompatiblePrinters": ["FDM"],
        "Attributes": [
            ("finish", "smooth"),
            ("articulated", "yes"),
            ("beginner-friendly", "yes"),
            ("snap-fit", "yes"),
        ],
    },
    {
        "Name": "Flexi Rex Dinosaur",
        "ShortDescription": "Classic flexible T-Rex print-in-place, no supports, great first print",
        "LongDescription": (
            "The original flexi T-Rex updated for modern slicers. Prints fully assembled in a "
            "single session with no supports and no post-processing. The lattice-style joints "
            "give excellent flexibility and durability. Works brilliantly with TPU for a rubbery "
            "feel or PLA for a firmer pose-and-display result. A community staple and a perfect "
            "test print for dialling in a new printer."
        ),
        "Price": 2.99,
        "CategoryId": CATEGORY_IDS["figurines"],
        "Currency": "USD",
        "Settings": {
            "LayerHeight": 0.2,
            "InfillPercentage": 15,
            "NozzleSize": 4,
            "PrintTimeMinutes": 120,
            "FilamentUsedGrams": 42.0,
            "SupportsRequired": False,
        },
        "Size": {"WidthMm": 160, "HeightMm": 90, "DepthMm": 55},
        "Tags": ["dinosaur", "trex", "flexi", "print-in-place", "no-supports", "articulated", "beginner"],
        "SupportedMaterials": ["PLA", "TPU", "PETG"],
        "CompatiblePrinters": ["FDM"],
        "Attributes": [
            ("articulated", "yes"),
            ("beginner-friendly", "yes"),
            ("size", "160mm"),
            ("lightweight", "yes"),
        ],
    },
    {
        "Name": "Modular Desk Organizer System",
        "ShortDescription": "Stackable, customizable desk organizer with dovetail-jointed modules",
        "LongDescription": (
            "A complete modular desk organization system. Includes pen/pencil cup, deep storage "
            "tray, phone stand, cable routing channel, business card holder, and a mini-drawer. "
            "All modules connect via precision dovetail joints — no hardware required. Mix and "
            "match to build the exact configuration your desk needs. Print in your accent colour "
            "for a clean, professional look."
        ),
        "Price": 7.99,
        "CategoryId": CATEGORY_IDS["functional"],
        "Currency": "USD",
        "Settings": {
            "LayerHeight": 0.2,
            "InfillPercentage": 20,
            "NozzleSize": 4,
            "PrintTimeMinutes": 480,
            "FilamentUsedGrams": 210.0,
            "SupportsRequired": False,
        },
        "Size": {"WidthMm": 250, "HeightMm": 100, "DepthMm": 120},
        "Tags": ["desk", "organizer", "modular", "functional", "office", "storage"],
        "SupportedMaterials": ["PLA", "PETG"],
        "CompatiblePrinters": ["FDM"],
        "Attributes": [
            ("modular", "yes"),
            ("customizable", "yes"),
            ("functional", "yes"),
            ("desk-friendly", "yes"),
            ("color", "any"),
        ],
    },
    {
        "Name": "Parametric Cable Management Box",
        "ShortDescription": "Snap-lid cable management box for under-desk or wall mounting",
        "LongDescription": (
            "A clean rectangular cable management box with a snap-fit lid and optional wall-mount "
            "keyhole slots on the base. Designed to hide power bricks, adapters, and cable excess "
            "under a desk or behind a monitor. Ships in three sizes — small (1 brick), medium "
            "(2–3 bricks), and large (full power strip). PETG recommended for heat resistance "
            "near power adapters."
        ),
        "Price": 3.49,
        "CategoryId": CATEGORY_IDS["functional"],
        "Currency": "USD",
        "Settings": {
            "LayerHeight": 0.2,
            "InfillPercentage": 25,
            "NozzleSize": 4,
            "PrintTimeMinutes": 200,
            "FilamentUsedGrams": 130.0,
            "SupportsRequired": False,
        },
        "Size": {"WidthMm": 200, "HeightMm": 80, "DepthMm": 100},
        "Tags": ["cable", "management", "desk", "functional", "office", "wall-mount"],
        "SupportedMaterials": ["PLA", "PETG", "ABS"],
        "CompatiblePrinters": ["FDM"],
        "Attributes": [
            ("snap-fit", "yes"),
            ("functional", "yes"),
            ("desk-friendly", "yes"),
            ("modular", "yes"),
            ("finish", "smooth"),
        ],
    },
    {
        "Name": "Planetary Gear Set",
        "ShortDescription": "Precision-tolerance planetary gearbox with visible spinning gears",
        "LongDescription": (
            "A functional planetary gearbox printed in place with tight 0.2 mm tolerances. "
            "Features a sun gear, four planet gears, and a ring gear, all meshing smoothly after "
            "a light break-in rotation. Ideal for kinetic desk sculptures, educational props, and "
            "mechanical engineering demonstrations. Nylon or PETG strongly recommended for wear "
            "resistance. Requires a well-calibrated printer with 0.4 mm nozzle."
        ),
        "Price": 6.49,
        "CategoryId": CATEGORY_IDS["mechanical"],
        "Currency": "USD",
        "Settings": {
            "LayerHeight": 0.1,
            "InfillPercentage": 40,
            "NozzleSize": 4,
            "PrintTimeMinutes": 350,
            "FilamentUsedGrams": 95.0,
            "SupportsRequired": False,
        },
        "Size": {"WidthMm": 100, "HeightMm": 50, "DepthMm": 100},
        "Tags": ["gears", "planetary", "mechanical", "engineering", "functional", "kinetic"],
        "SupportedMaterials": ["PETG", "ABS", "Nylon"],
        "CompatiblePrinters": ["FDM"],
        "Attributes": [
            ("high-detail", "yes"),
            ("functional", "yes"),
            ("layer_height", "0.1mm"),
            ("nozzle_size", "0.4mm"),
        ],
    },
    {
        "Name": "Self-Watering Planter with Drainage Reservoir",
        "ShortDescription": "Eco-friendly self-watering planter with passive wicking system",
        "LongDescription": (
            "A two-part self-watering planter system. The inner pot holds soil and has a wicking "
            "cord that draws moisture up from the outer reservoir. Recommended for small succulents, "
            "herbs, or desk plants. Print with recycled PLA, wood-fill, or standard PLA+ for the "
            "best eco-friendly results. The reservoir holds approximately 200 ml of water."
        ),
        "Price": 3.99,
        "CategoryId": CATEGORY_IDS["home_garden"],
        "Currency": "USD",
        "Settings": {
            "LayerHeight": 0.2,
            "InfillPercentage": 3,
            "NozzleSize": 4,
            "PrintTimeMinutes": 180,
            "FilamentUsedGrams": 75.0,
            "SupportsRequired": False,
        },
        "Size": {"WidthMm": 100, "HeightMm": 130, "DepthMm": 100},
        "Tags": ["planter", "eco-friendly", "self-watering", "garden", "sustainable", "plants"],
        "SupportedMaterials": ["PLA", "PETG"],
        "CompatiblePrinters": ["FDM"],
        "Attributes": [
            ("eco-friendly", "yes"),
            ("functional", "yes"),
            ("finish", "matte"),
            ("lightweight", "yes"),
        ],
    },
    {
        "Name": "Articulated Octopus",
        "ShortDescription": "Eight-tentacled articulated octopus, fully poseable, print-in-place",
        "LongDescription": (
            "A highly detailed articulated octopus with eight independently moving tentacles. "
            "Prints fully assembled in one shot with no supports. Each tentacle has 12 articulated "
            "segments with smooth ball-and-socket joints. TPU gives a satisfying rubbery feel; "
            "PLA gives a firm poseable result. Popular gifting and desk-toy model."
        ),
        "Price": 4.49,
        "CategoryId": CATEGORY_IDS["figurines"],
        "Currency": "USD",
        "Settings": {
            "LayerHeight": 0.15,
            "InfillPercentage": 15,
            "NozzleSize": 4,
            "PrintTimeMinutes": 300,
            "FilamentUsedGrams": 110.0,
            "SupportsRequired": False,
        },
        "Size": {"WidthMm": 200, "HeightMm": 80, "DepthMm": 200},
        "Tags": ["octopus", "articulated", "print-in-place", "no-supports", "sea", "poseable"],
        "SupportedMaterials": ["PLA", "TPU", "PETG"],
        "CompatiblePrinters": ["FDM"],
        "Attributes": [
            ("articulated", "yes"),
            ("snap-fit", "yes"),
            ("beginner-friendly", "yes"),
            ("high-detail", "yes"),
        ],
    },
    {
        "Name": "Resin Miniature Knight",
        "ShortDescription": "Highly detailed 28mm scale knight miniature for tabletop gaming, optimised for SLA/MSLA",
        "LongDescription": (
            "A 28 mm heroic-scale knight miniature with exceptional surface detail — chainmail "
            "texture, engraved pauldrons, visor grille, and weapon detail down to 0.05 mm. "
            "Designed specifically for SLA/MSLA resin printers. Includes pre-supported and "
            "unsupported versions. Perfect for tabletop RPGs, wargaming, or display painting. "
            "Hollow interior reduces resin consumption."
        ),
        "Price": 2.49,
        "CategoryId": CATEGORY_IDS["decor"],
        "Currency": "USD",
        "Settings": {
            "LayerHeight": 0.05,
            "InfillPercentage": 0,
            "NozzleSize": 0,
            "PrintTimeMinutes": 180,
            "FilamentUsedGrams": 12.0,
            "SupportsRequired": True,
        },
        "Size": {"WidthMm": 25, "HeightMm": 50, "DepthMm": 20},
        "Tags": ["miniature", "knight", "tabletop", "wargaming", "resin", "28mm", "rpg"],
        "SupportedMaterials": ["Resin"],
        "CompatiblePrinters": ["SLA"],
        "Attributes": [
            ("high-detail", "yes"),
            ("miniature", "yes"),
            ("finish", "smooth"),
            ("size", "28mm"),
        ],
    },
    {
        "Name": "Magnetic Snap Lid Storage Container",
        "ShortDescription": "Stackable storage container with embedded magnet snap lid, available in four sizes",
        "LongDescription": (
            "A clean stackable storage container family with friction-fit lids reinforced by "
            "embedded 6x2 mm neodymium magnets. Four sizes — micro, small, medium, large — all "
            "stack together neatly. Ideal for storing screws, resistors, hobby supplies, jewelry, "
            "or desk accessories. PETG recommended for durability; PLA works well for light loads."
        ),
        "Price": 4.99,
        "CategoryId": CATEGORY_IDS["functional"],
        "Currency": "USD",
        "Settings": {
            "LayerHeight": 0.2,
            "InfillPercentage": 30,
            "NozzleSize": 4,
            "PrintTimeMinutes": 220,
            "FilamentUsedGrams": 90.0,
            "SupportsRequired": False,
        },
        "Size": {"WidthMm": 80, "HeightMm": 50, "DepthMm": 80},
        "Tags": ["storage", "container", "magnetic", "stackable", "modular", "functional"],
        "SupportedMaterials": ["PLA", "PETG"],
        "CompatiblePrinters": ["FDM"],
        "Attributes": [
            ("modular", "yes"),
            ("snap-fit", "yes"),
            ("functional", "yes"),
            ("customizable", "yes"),
        ],
    },
    {
        "Name": "Adjustable Phone and Tablet Stand",
        "ShortDescription": "Minimalist adjustable phone/tablet desk stand, two viewing angles, cable slot",
        "LongDescription": (
            "A slim minimalist stand that holds phones and tablets in portrait or landscape "
            "orientation at 65° or 75°. Features a cable routing slot at the base for charging "
            "without removing the device. Prints in three parts (base, arm, lip) with no "
            "supports and joins with friction fits. Compatible with devices up to 13 mm thick "
            "(most phones with thin cases)."
        ),
        "Price": 2.99,
        "CategoryId": CATEGORY_IDS["accessories"],
        "Currency": "USD",
        "Settings": {
            "LayerHeight": 0.2,
            "InfillPercentage": 20,
            "NozzleSize": 4,
            "PrintTimeMinutes": 90,
            "FilamentUsedGrams": 35.0,
            "SupportsRequired": False,
        },
        "Size": {"WidthMm": 100, "HeightMm": 110, "DepthMm": 80},
        "Tags": ["phone", "tablet", "stand", "desk", "accessories", "functional", "minimalist"],
        "SupportedMaterials": ["PLA", "PETG"],
        "CompatiblePrinters": ["FDM"],
        "Attributes": [
            ("functional", "yes"),
            ("desk-friendly", "yes"),
            ("beginner-friendly", "yes"),
            ("customizable", "yes"),
            ("finish", "smooth"),
        ],
    },
]

# Relations: (product_name, related_product_name, relation_type)
RELATIONS = [
    ("Articulated Dragon", "Flexi Rex Dinosaur", "similar"),
    ("Articulated Dragon", "Articulated Octopus", "similar"),
    ("Flexi Rex Dinosaur", "Articulated Octopus", "similar"),
    ("Modular Desk Organizer System", "Parametric Cable Management Box", "complementary"),
    ("Modular Desk Organizer System", "Magnetic Snap Lid Storage Container", "similar"),
    ("Modular Desk Organizer System", "Adjustable Phone and Tablet Stand", "complementary"),
    ("Adjustable Phone and Tablet Stand", "Parametric Cable Management Box", "complementary"),
    ("Planetary Gear Set", "Articulated Dragon", "similar"),
    ("Resin Miniature Knight", "Articulated Dragon", "similar"),
]


# ──────────────────────────────────────────────
# HTTP helpers
# ──────────────────────────────────────────────

async def create_product(client: httpx.AsyncClient, gateway_url: str, product: dict) -> str | None:
    payload = {k: v for k, v in product.items() if k != "Attributes"}
    resp = await client.post(f"{gateway_url}/products/", json=payload)
    if resp.status_code not in (200, 201):
        print(f"  ✗ Failed to create '{product['Name']}': {resp.status_code} {resp.text}")
        return None
    product_id = resp.json()["productId"]
    print(f"  ✓ Created '{product['Name']}' → {product_id}")
    return product_id


async def add_attribute(
    client: httpx.AsyncClient, gateway_url: str, product_id: str, key: str, value: str
) -> None:
    resp = await client.post(
        f"{gateway_url}/products/{product_id}/attributes",
        json={"Key": key, "Value": value},
    )
    if resp.status_code not in (200, 201):
        print(f"    ✗ Attribute '{key}': {resp.status_code} {resp.text}")
    else:
        print(f"    + attr {key}={value}")


async def add_relation(
    client: httpx.AsyncClient,
    gateway_url: str,
    product_id: str,
    related_product_id: str,
    relation_type: str,
) -> None:
    resp = await client.post(
        f"{gateway_url}/products/{product_id}/relations",
        json={"RelatedProductId": related_product_id, "Type": relation_type},
    )
    if resp.status_code not in (200, 201):
        print(f"    ✗ Relation '{relation_type}': {resp.status_code} {resp.text}")
    else:
        print(f"    + relation {relation_type} → {related_product_id}")


# ──────────────────────────────────────────────
# Main
# ──────────────────────────────────────────────

async def seed(gateway_url: str) -> None:
    print(f"\nSeeding catalog via API Gateway: {gateway_url}\n")

    # index product_name → product_id for relation wiring
    created: dict[str, str] = {}

    async with httpx.AsyncClient(timeout=30.0) as client:
        # ── 1. Create all products ──────────────────────
        print("── Creating products ──────────────────────────────")
        for product in PRODUCTS:
            product_id = await create_product(client, gateway_url, product)
            if product_id:
                created[product["Name"]] = product_id

        if not created:
            print("\n✗ No products were created. Is the gateway reachable?")
            sys.exit(1)

        # Small delay to let the catalog service settle
        await asyncio.sleep(1)

        # ── 2. Add attributes ───────────────────────────
        print("\n── Adding attributes ──────────────────────────────")
        for product in PRODUCTS:
            product_id = created.get(product["Name"])
            if not product_id:
                continue
            print(f"  {product['Name']}:")
            for key, value in product.get("Attributes", []):
                await add_attribute(client, gateway_url, product_id, key, value)

        # ── 3. Add relations ────────────────────────────
        print("\n── Adding relations ───────────────────────────────")
        for source_name, target_name, rel_type in RELATIONS:
            source_id = created.get(source_name)
            target_id = created.get(target_name)
            if not source_id or not target_id:
                print(f"  ✗ Skipping relation '{source_name}' → '{target_name}': product not found")
                continue
            print(f"  {source_name[:35]:<35} --{rel_type}--> {target_name}")
            await add_relation(client, gateway_url, source_id, target_id, rel_type)

    print(f"\n✓ Done. Created {len(created)}/{len(PRODUCTS)} products.")
    print("  The GraphRAG worker will ingest the RabbitMQ events automatically.")
    print("  Wait a few seconds, then query: http://localhost:8100/api/search\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Seed the catalog via the API Gateway")
    parser.add_argument(
        "--gateway-url",
        default=DEFAULT_GATEWAY_URL,
        help=f"API Gateway base URL (default: {DEFAULT_GATEWAY_URL})",
    )
    args = parser.parse_args()
    asyncio.run(seed(args.gateway_url))

