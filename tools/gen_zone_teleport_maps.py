#!/usr/bin/env python3
"""Regenerate the zone teleport map SVGs (static/zonemaps/*.svg):

  * background: the zone's REAL terrain, read from the game's layer files
    (altitude.NNNN.bin, 2 bytes/tile little-endian; see
    content/formats/zone_files.md), sourced in this order:
      1. $OP_ASSETS_DIR/custom-layers/  (default: ../PerpetuumServer2)
      2. the .gbf archives next to it (decoded with the server's own
         script/extract_gbf.py decoder — GXY2 format)
    Two display modes are rendered per zone (512x512 PNGs, referenced by
    the SVG and switchable in the UI by static/zone-map.js):
      height  hillshaded monochrome heightmap (real island shapes)
      color   altitude color ramp + coastline (derived from the altitude
               band, so the line sits exactly where the color changes)
    Both modes also show the zone's roads (the Highway flag, 1<<7, of the
    control layer — TerrainControlFlags from the server code) in grey.
    Road tiles below sea level are drawn in a darker grey.
    (The blocks layer's Island flag turned out to be too sparse to use as
    a land mask — it marks only a fraction of land tiles — hence the
    altitude-derived coastline.)
    Zones without layer data anywhere (e.g. the training zone) fall back
    to a deterministic procedural heightmap (fBm value noise +
    hillshading, seeded per zone) embedded as base64.
  * an emphasized border around the zone.
  * zones whose layer data is not present (e.g. CI, where the original
    zones' layers only ship in the game client's Perpetuum.gbf) keep their
    committed derived PNGs from static/zonemaps-fallback/<slug>/ instead of
    downgrading to the procedural placeholder.
  * interactivity: teleport columns, landing spots ("from …") and exit
    gates ("exit → …") whose destination/origin resolves to a zone page
    are wrapped in <a href="/zones/<slug>/"> so they are clickable once the
    SVG is inlined by static/zone-map.js (zoom/pan is then picked up
    automatically by static/map.js).
  * two extra derived PNGs per zone, for the pages that embed many zones
    at once (both derived from the same altitude data, so the island shape
    matches the full maps):
      world.png  128×128 color-mode terrain — the world map (content/zones/
                 map.md) draws every zone with its real terrain instead of
                 a flat shape
      thumb.png  256×256 coastline-only outline (bold island border in the
                 galaxy family color, no points of interest) — the zone
                 cards on the world page

The input SVGs are produced by generator/Perpetuum.WikiGenerate
(ZoneMapSvg.cs) from the live database. This script only rewrites the
static assets, so it needs no database. Pure standard library (plus the
server's extract_gbf.py, also stdlib, when a .gbf fallback is needed) —
run it locally (`python3 tools/gen_zone_teleport_maps.py`) or via
`make zonemaps` (containerized). Zones are independent, so the script
spawns one process per CPU core (multiprocessing — the per-tile loops are
Python bytecode, so threads would not help).

Note: when the C# generator is re-run it will overwrite these files;
re-run this script afterwards (or port this logic into ZoneMapSvg.cs).
"""
import base64
import hashlib
import os
import random
import re
import struct
import zlib
from itertools import accumulate
from multiprocessing import Pool

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ZONEMAPS = os.path.join(ROOT, "static", "zonemaps")
# committed derived PNGs for zones whose layer data is not fetchable in CI
# (it only ships in the game client's Perpetuum.gbf): when a zone has no
# layer data but a fallback pair exists here, the tool copies it into
# static/zonemaps/<slug>/ instead of downgrading to the fBm placeholder
ZONEMAPS_FALLBACK = os.path.join(ROOT, "static", "zonemaps-fallback")
ZONE_INDEX = os.path.join(ROOT, "content", "zones", "zone-index.md")
# the game assets live in the sibling server checkout
ASSETS = os.environ.get("OP_ASSETS_DIR",
                        os.path.join(ROOT, "..", "PerpetuumServer2"))

# ---------------------------------------------------------------- name map

_ROW = re.compile(
    r"\|\s*(\d+)\s*\|\s*\[([^\]]+)\]\((/zones/[^/]+)/\)\s*\|[^|]*\|[^|]*\|[^|]*\|\s*(\d+)×(\d+)\s*\|")


def load_zone_meta():
    """From the zone index table: name -> /zones/<slug>/ and
    slug -> (zone_id, width, height)."""
    text = open(ZONE_INDEX, encoding="utf-8").read()
    name2slug, slug2id, slug2size = {}, {}, {}
    for zid, name, slug, w, h in _ROW.findall(text):
        # the table links carry the trailing slash; the regex group stops
        # before it, so put it back — Zola pages are /zones/<slug>/
        name2slug.setdefault(name.strip(), slug + "/")
        base = slug.rstrip("/").split("/")[-1] + "/"  # /zones/<slug>/ -> <slug>/
        slug2id.setdefault(base, int(zid))
        slug2size.setdefault(base, (int(w), int(h)))
    return name2slug, slug2id, slug2size


def resolve(names, name2slug):
    """First label part that resolves to a zone page; None if unresolvable."""
    for n in names:
        n = n.strip().rstrip("…").strip()
        if n and n in name2slug:
            return name2slug[n]
    return None

# ------------------------------------------------- real terrain (from gbf)

_GBFS = []  # lazy: [(path, GXY2Archive)]


def _gbf_archives():
    global _GBFS
    if not _GBFS:
        try:
            sys = __import__("sys")
            script_dir = os.path.join(ASSETS, "script")
            if os.path.isdir(script_dir):
                sys.path.insert(0, script_dir)
            from extract_gbf import GXY2Archive  # the server's own decoder
        except Exception:
            return []
        for f in sorted(os.listdir(ASSETS)):
            if not f.endswith(".gbf"):
                continue
            p = os.path.join(ASSETS, f)
            try:
                _GBFS.append((p, GXY2Archive(p)))
            except Exception as e:
                print(f"  [!] skipping {f}: {e}")
    return _GBFS


def _layer_bytes(name):
    """Raw bytes of a layer entry (e.g. 'altitude0004') from a .gbf, or None."""
    for _, arc in _gbf_archives():
        for e in arc.entries:
            if e["name"] == name:
                with open(arc.filepath, "rb") as fh:
                    return arc.extract_file(e, fh)
    return None


# TerrainControlFlags (server: Perpetuum/Zones/Terrains/TerrainControlFlags.cs);
# the on-disk control layer is one ushort per tile (little-endian).
FLAG_HIGHWAY = 1 << 7
# sea level for the ramp (shore transition) — roads below it are over water
# the ramp has no colors between 950 (shore) and 1500 (grass) — the real
# sea/land boundary sits in that gap; these thresholds split it
SEA_LEVEL = 1200    # below: over water (dark road grey)
SEA_COAST = 1200    # below: sea (coastline derived from this crossing)


def load_terrain(zid, w, h, size):
    """(alt, coast, roads, None) from the game's layers, size×size grids.

    alt: box-averaged elevation. coast: 1 on cells where the sea/land
    boundary runs (altitude crossing SEA_COAST). roads: 1 on cells
    containing a Highway tile (control layer). (None, ...) when the zone
    has no layer data anywhere.
    """
    id4 = f"{zid:04d}"
    alt_path = os.path.join(ASSETS, "custom-layers", f"altitude.{id4}.bin")
    ctl_path = os.path.join(ASSETS, "custom-layers", f"control.{id4}.bin")
    alt_raw = ctl_raw = None
    if os.path.isfile(alt_path):
        alt_raw = open(alt_path, "rb").read()
        if os.path.isfile(ctl_path):
            ctl_raw = open(ctl_path, "rb").read()
    else:
        alt_raw = _layer_bytes(f"altitude{id4}")
        if ctl_raw is None and alt_raw is not None:
            ctl_raw = _layer_bytes(f"control{id4}")
    if alt_raw is None:
        return None, None, None, None
    if len(alt_raw) != w * h * 2:
        print(f"  [!] zone {id4}: altitude layer size {len(alt_raw)} != {w * h * 2} — skipped")
        return None, None, None, None

    fx, fy = w // size, h // size
    acc = [[0.0] * w for _ in range(size)]
    roads = [[0] * size for _ in range(size)] if ctl_raw is not None and len(ctl_raw) == w * h * 2 else None
    for y in range(h):
        row = struct.unpack_from(f"<{w}H", alt_raw, y * w * 2)
        crow = struct.unpack_from(f"<{w}H", ctl_raw, y * w * 2) if roads is not None else None
        ay = acc[y // fy]
        cy = y // fy
        rrow = roads[cy] if roads is not None else None
        for x in range(w):
            ay[x] += row[x]
            if rrow is not None and crow[x] & FLAG_HIGHWAY:
                rrow[x // fx] = 1
    alt = []
    for oy in range(size):
        ps = [0]
        ps.extend(accumulate(acc[oy]))  # ps[i] = sum of columns [0, i)
        alt.append([(ps[(ox + 1) * fx] - ps[ox * fx]) / (fx * fy) for ox in range(size)])

    return alt, coast_mask(alt), roads, None


def coast_mask(alt):
    """Coastline cells of a box-averaged altitude grid: where the grid
    crosses the sea/land boundary (sits exactly on the rendered color
    change)."""
    size = len(alt)
    sea = [[v < SEA_COAST for v in row] for row in alt]
    coast = [[0] * size for _ in range(size)]
    for y in range(size):
        for x in range(size):
            s = sea[y][x]
            if (x > 0 and sea[y][x - 1] != s) or (y > 0 and sea[y - 1][x] != s) \
              or (x < size - 1 and sea[y][x + 1] != s) or (y < size - 1 and sea[y + 1][x] != s):
                coast[y][x] = 1
    return coast


RAMP = [  # (elevation, rgb) — deep water to snow line
    (0,     (13, 34, 66)),
    (350,   (21, 55, 95)),
    (750,   (46, 86, 124)),
    (950,   (124, 110, 74)),
    (1500,  (72, 104, 60)),
    (3800,  (94, 122, 58)),
    (7500,  (97, 103, 55)),
    (12500, (112, 99, 77)),
    (18500, (130, 125, 118)),
    (23500, (216, 221, 229)),
]


def _ramp(v):
    if v <= RAMP[0][0]:
        return RAMP[0][1]
    for i in range(1, len(RAMP)):
        if v <= RAMP[i][0]:
            v0, c0 = RAMP[i - 1]
            v1, c1 = RAMP[i]
            t = (v - v0) / (v1 - v0)
            return tuple(int(c0[k] + (c1[k] - c0[k]) * t) for k in range(3))
    return RAMP[-1][1]


def _shade_at(alt, y, x):
    """Hillshade from the gradient, light from the north-west."""
    n = len(alt)
    xl = alt[y][x - 1] if x > 0 else alt[y][x]
    xr = alt[y][x + 1] if x < n - 1 else alt[y][x]
    yu = alt[y - 1][x] if y > 0 else alt[y][x]
    yd = alt[y + 1][x] if y < n - 1 else alt[y][x]
    s = 1.0 + (xl - xr + yu - yd) * 0.00035
    return max(0.62, min(1.35, s))


# road grey: light enough to read on land, darker over water (the game lays
# highway tiles out to the teleport columns standing off-coast)
ROAD_LAND = (176, 182, 194)
ROAD_WATER = (104, 112, 126)
ROAD_LAND_H = (150, 158, 172)
ROAD_WATER_H = (96, 104, 118)


def height_png(alt, roads, _isl):
    """Hillshaded monochrome heightmap (real terrain) as PNG bytes."""
    size = len(alt)
    px = bytearray()
    vmax = max(max(r) for r in alt)
    for y in range(size):
        row = alt[y]
        rrow = roads[y] if roads else None
        for x in range(size):
            if rrow and rrow[x]:
                c = ROAD_LAND_H if row[x] >= SEA_LEVEL else ROAD_WATER_H
                px += bytes((c[0], c[1], c[2], 255))
                continue
            sh = _shade_at(alt, y, x)
            t = (row[x] / vmax) ** 0.65 if vmax else 0.0
            r = int(min(255, (LO[0] + (HI[0] - LO[0]) * t) * sh))
            g = int(min(255, (LO[1] + (HI[1] - LO[1]) * t) * sh))
            b = int(min(255, (LO[2] + (HI[2] - LO[2]) * t) * sh))
            px += bytes((r, g, b, 255))
    return png_encode(size, size, bytes(px))


def color_png(alt, coast, roads, _isl):
    """Altitude color ramp + roads + island coastline as PNG bytes."""
    size = len(alt)
    px = bytearray()
    for y in range(size):
        row = alt[y]
        crow = coast[y] if coast else None
        rrow = roads[y] if roads else None
        for x in range(size):
            if crow and crow[x]:
                px += bytes((191, 232, 255, 255))  # island border
                continue
            if rrow and rrow[x]:
                c = ROAD_LAND if row[x] >= SEA_LEVEL else ROAD_WATER
                px += bytes((c[0], c[1], c[2], 255))
                continue
            sh = _shade_at(alt, y, x)
            r, g, b = _ramp(row[x])
            px += bytes((int(min(255, r * sh)), int(min(255, g * sh)), int(min(255, b * sh)), 255))
    return png_encode(size, size, bytes(px))

# ------------------------------------------- world map + index-card assets

FAM_PARTS = [("gamma", "#a78bfa"), ("tm", "#41d3ff"), ("ics", "#6ee7a0"), ("asi", "#f5a05a")]


def fam_color(zone_name):
    """Galaxy family color, same scheme as the C# generators."""
    n = zone_name.lower()
    for part, c in FAM_PARTS:
        if part in n:
            return c
    return "#c8d2e0"


def downsample(grid, factor):
    """Box-average an integer grid by an integer factor."""
    n = len(grid)
    m = n // factor
    out = [[0.0] * m for _ in range(m)]
    for y in range(m):
        oy = y * factor
        orow = out[y]
        for x in range(m):
            ox = x * factor
            t = 0.0
            for dy in range(factor):
                row = grid[oy + dy]
                for dx in range(factor):
                    t += row[ox + dx]
            orow[x] = t / (factor * factor)
    return out


def downsample_road(roads, factor):
    """Any road tile inside the box keeps the road."""
    n = len(roads)
    m = n // factor
    out = [[0] * m for _ in range(m)]
    for y in range(m):
        oy = y * factor
        orow = out[y]
        for x in range(m):
            ox = x * factor
            for dy in range(factor):
                row = roads[oy + dy]
                for dx in range(factor):
                    if row[ox + dx]:
                        orow[x] = 1
                        break
    return out


def world_png(alt, roads):
    """128×128 color-mode terrain for the world map (the zone's real map,
    downscaled)."""
    size = len(alt)
    factor = max(1, size // 128)
    if factor > 1:
        alt = downsample(alt, factor)
        roads = downsample_road(roads, factor) if roads else None
    return color_png(alt, coast_mask(alt), roads, None)


def thumb_png(coast, color):
    """Coastline-only outline for the zone cards: bold island border in the
    family color, dark background, no points of interest."""
    size = len(coast)
    r = int(color[1:3], 16); g = int(color[3:5], 16); b = int(color[5:7], 16)
    px = bytearray()
    for y in range(size):
        row = coast[y]
        up = coast[y - 1] if y > 0 else None
        dn = coast[y + 1] if y < size - 1 else None
        for x in range(size):
            # dilate by one cell: a 1-cell coastline reads bold at card size
            on = (row[x]
                  or (x > 0 and row[x - 1]) or (x < size - 1 and row[x + 1])
                  or (up is not None and up[x]) or (dn is not None and dn[x]))
            px += bytes((r, g, b, 255) if on else (16, 21, 31, 255))
    return png_encode(size, size, bytes(px))


def png_decode(b):
    """Decode the filter-0 RGBA PNGs this tool itself writes (the committed
    fallback set) into a pixel grid."""
    assert b[:8] == b"\x89PNG\r\n\x1a\n"
    pos, idat, w = 8, b"", None
    while pos < len(b):
        (ln,) = struct.unpack(">I", b[pos:pos + 4])
        tag = b[pos + 4:pos + 8]
        if tag == b"IHDR":
            w, h, _bd, _ct = struct.unpack(">IIBB", b[pos + 8:pos + 18])
        elif tag == b"IDAT":
            idat += b[pos + 8:pos + 8 + ln]
        elif tag == b"IEND":
            break
        pos += 12 + ln
    raw = zlib.decompress(idat)
    stride = w * 4
    return w, [[list(struct.unpack("<%dB" % (w * 4), raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)]))
                for y in range(h)]]


def _rgb_downsample(px, factor):
    n = len(px)
    m = n // factor
    out = [[None] * m for _ in range(m)]
    for y in range(m):
        oy = y * factor
        orow = out[y]
        for x in range(m):
            ox = x * factor
            acc = [0, 0, 0]
            for dy in range(factor):
                row = px[oy + dy]
                for dx in range(factor):
                    p = row[ox + dx]
                    acc[0] += p[0]; acc[1] += p[1]; acc[2] += p[2]
            k = 1.0 / (factor * factor)
            orow[x] = [int(a * k) for a in acc]
    return out


def fallback_world_thumb(color_bytes, zone_name):
    """world.png + thumb.png derived from the committed fallback color.png
    (no altitude data for this zone): the terrain is a straight downscale,
    the coastline the sea/land boundary detected from the rendered colors
    (the deep-water ramp color vs. everything else)."""
    w, px = png_decode(color_bytes)
    world = png_encode(128, 128, b"".join(
        bytes(p[:3] + (255,)) for p in _rgb_downsample(px, max(1, w // 128))))
    deep = (13, 34, 66)  # the ramp's deep-water color
    def sea(p):
        return (p[0] - deep[0]) ** 2 + (p[1] - deep[1]) ** 2 + (p[2] - deep[2]) ** 2 < 60 * 60
    m = len(px)
    sea_m = [[sea(p) for p in row] for row in px]
    coast = [[0] * m for _ in range(m)]
    for y in range(m):
        for x in range(m):
            s = sea_m[y][x]
            if (x > 0 and sea_m[y][x - 1] != s) or (y > 0 and sea_m[y - 1][x] != s) \
              or (x < m - 1 and sea_m[y][x + 1] != s) or (y < m - 1 and sea_m[y + 1][x] != s):
                coast[y][x] = 1
    # upscale the mask 2x for the 256 thumb
    coast2 = [[0] * (m * 2) for _ in range(m * 2)]
    for y in range(m):
        for x in range(m):
            if coast[y][x]:
                for dy in range(2):
                    for dx in range(2):
                        coast2[y * 2 + dy][x * 2 + dx] = 1
    return world, thumb_png(coast2, fam_color(zone_name))


FBM_SEA = 0.45  # fBm value below which the procedural placeholder is water


def fbm_world_thumb(zone_name):
    """world.png + thumb.png for zones with no layer data anywhere (the
    procedural fBm placeholder): both derived from one 256×256 field so the
    island shape matches between the two."""
    seed = int.from_bytes(hashlib.md5(zone_name.encode("utf-8")).digest()[:8], "big")
    img = fbm(256, seed)
    sea = [[v < FBM_SEA for v in row] for row in img]
    coast = [[0] * 256 for _ in range(256)]
    for y in range(256):
        for x in range(256):
            s = sea[y][x]
            if (x > 0 and sea[y][x - 1] != s) or (y > 0 and sea[y - 1][x] != s) \
              or (x < 255 and sea[y][x + 1] != s) or (y < 255 and sea[y + 1][x] != s):
                coast[y][x] = 1
    alt128 = downsample([[v * 23500.0 for v in row] for row in img], 2)
    px = bytearray()
    for y in range(128):
        row = alt128[y]
        for x in range(128):
            r, g, b = _ramp(row[x])
            px += bytes((r, g, b, 255))
    return png_encode(128, 128, bytes(px)), thumb_png(coast, fam_color(zone_name))
# ------------------------------------------------------------- heightmap

def fbm(size, seed, octaves=5, base_freq=3):
    rnd = random.Random(seed)
    img = [[0.0] * size for _ in range(size)]
    amp, freq, norm = 1.0, base_freq, 0.0
    for _ in range(octaves):
        gw = freq + 1
        grid = [[rnd.random() for _ in range(gw)] for _ in range(gw)]
        for y in range(size):
            gy = y * freq / (size - 1)
            y0 = min(int(gy), gw - 2)
            fy = gy - y0
            row = img[y]
            for x in range(size):
                gx = x * freq / (size - 1)
                x0 = min(int(gx), gw - 2)
                fx = gx - x0
                v = (grid[y0][x0] * (1 - fx) + grid[y0][x0 + 1] * fx) * (1 - fy) \
                  + (grid[y0 + 1][x0] * (1 - fx) + grid[y0 + 1][x0 + 1] * fx) * fy
                row[x] += v * amp
        norm += amp
        amp *= 0.5
        freq *= 2
    for y in range(size):
        row = img[y]
        for x in range(size):
            row[x] /= norm
    return img


LO = (10, 16, 30)     # deep lowland
HI = (66, 92, 128)    # high ground


def png_encode(w, h, rgba):
    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)
    raw = b"".join(b"\x00" + rgba[y * w * 4:(y + 1) * w * 4] for y in range(h))
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 6))
            + chunk(b"IEND", b""))


def heightmap_png(zone_name, size=512):
    """Deterministic hillshaded heightmap as a PNG (RGBA) byte string."""
    seed = int.from_bytes(hashlib.md5(zone_name.encode("utf-8")).digest()[:8], "big")
    img = fbm(size, seed)
    px = bytearray()
    for y in range(size):
        for x in range(size):
            v = img[y][x]
            xl = img[y][max(x - 1, 0)]
            xr = img[y][min(x + 1, size - 1)]
            yu = img[max(y - 1, 0)][x]
            yd = img[min(y + 1, size - 1)][x]
            shade = 1.0 + (xl - xr + (yu - yd)) * 2.4
            shade = max(0.68, min(1.28, shade))
            t = max(0.0, min(1.0, v))
            r = int(min(255, (LO[0] + (HI[0] - LO[0]) * t) * shade))
            g = int(min(255, (LO[1] + (HI[1] - LO[1]) * t) * shade))
            b = int(min(255, (LO[2] + (HI[2] - LO[2]) * t) * shade))
            px += bytes((r, g, b, 255))
    return png_encode(size, size, bytes(px))

# ------------------------------------------------------------ svg rewrite

ATTR = re.compile(r'([a-z-]+)="([^"]*)"')
ITEM = re.compile(r"<(rect|line|circle|path)\b([^>]*?)/>|<text\b([^>]*?)>(.*?)</text>", re.S)


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")


def _fmt(v):
    """18.0 -> '18', 2.25 -> '2.25'"""
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    return s or "0"


def markup(kind, at, txt=None):
    inner = " ".join(f'{k}="{v}"' for k, v in at.items())
    if txt is None:
        return f"<{kind} {inner}/>"
    return f"<text {inner}>{txt}</text>"


def parse(svg_text):
    """(w, h, zone, [(kind, attrs, text-or-None), ...]) in document order."""
    m = re.search(r'viewBox="0 0 (\d+) (\d+)"', svg_text)
    w, h = int(m.group(1)), int(m.group(2))
    zone = re.search(r'aria-label="Teleport map of ([^"]+)"', svg_text).group(1)
    els = []
    for tm in ITEM.finditer(svg_text):
        if tm.group(3) is not None:  # <text> alternative
            els.append(("text", dict(ATTR.findall(tm.group(3))), tm.group(4)))
        else:
            els.append((tm.group(1), dict(ATTR.findall(tm.group(2))), None))
    return w, h, zone, els


def label_names(kind, label):
    """The zone names a label refers to (link targets)."""
    if kind == "spot" and label.startswith("from "):
        return label[5:].split(",")
    if kind == "gate" and label.startswith("exit → "):
        return [label[7:]]
    return label.split(",")


def rebuild(path, name2slug, slug2id, slug2size):
    svg_text = open(path, encoding="utf-8").read()
    w, h, zone, els = parse(svg_text)
    f = w / 2048.0
    slug = os.path.basename(path)[:-4] + "/"  # e.g. "zone-asi-a-real/"
    # the POI enlargement below is multiplicative — the marker makes the
    # script idempotent (a re-run passes the already-scaled sizes through)
    already = 'data-zm-v2=' in svg_text

    out = []
    out.append(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="640" '
               f'height="{int(640.0 * h / w)}" role="img" aria-label="Teleport map of {esc(zone)}" '
               f'data-zm-v2="1">')
    # zone extent, then the background: real terrain (two switchable modes —
    # static/zone-map.js swaps the images) or, when the zone has no layer
    # data, an embedded procedural heightmap.
    out.append(f'  <rect x="0" y="0" width="{w}" height="{h}" rx="{16 * f}" fill="#10151f" stroke="#39445a" stroke-width="{2 * f}"/>')
    zid = slug2id.get(slug)
    real = (None, None, None, None)
    if zid is not None:
        try:
            # never upsample: tiny zones (256×256) keep their native size
            real = load_terrain(zid, w, h, min(512, w, h)) or real
        except Exception as e:
            print(f"  [!] {slug}: {e}")
    # committed derived PNGs for zones whose layer data is not fetchable in
    # CI (it only ships in the game client's Perpetuum.gbf)
    fb = os.path.join(ZONEMAPS_FALLBACK, slug)
    have_fallback = (os.path.isfile(os.path.join(fb, "height.png"))
                     and os.path.isfile(os.path.join(fb, "color.png")))
    if real[0] is not None:
        alt, coast, roads, isl = real
        d = os.path.join(ZONEMAPS, slug)
        os.makedirs(d, exist_ok=True)
        open(os.path.join(d, "height.png"), "wb").write(height_png(alt, roads, isl))
        open(os.path.join(d, "color.png"), "wb").write(color_png(alt, coast, roads, isl))
        # world-map node + index-card thumbnail (same altitude data)
        open(os.path.join(d, "world.png"), "wb").write(world_png(alt, roads))
        # the thumb targets 256×256 (a native-512 zone would otherwise draw
        # its full-res coastline, far heavier than needed for a card)
        tsize = len(alt)
        tf = max(1, tsize // 256)
        if tf > 1:
            alt_t = downsample(alt, tf)
            coast_t = coast_mask(alt_t)
        else:
            alt_t, coast_t = alt, coast
        open(os.path.join(d, "thumb.png"), "wb").write(thumb_png(coast_t, fam_color(zone)))
    elif have_fallback:
        # no layer data here: keep the committed derived PNGs (copied into
        # place so the SVG's /zonemaps/<slug>/ URLs resolve)
        d = os.path.join(ZONEMAPS, slug)
        os.makedirs(d, exist_ok=True)
        for name in ("height.png", "color.png"):
            open(os.path.join(d, name), "wb").write(open(os.path.join(fb, name), "rb").read())
        world, thumb = fallback_world_thumb(open(os.path.join(fb, "color.png"), "rb").read(), zone)
        open(os.path.join(d, "world.png"), "wb").write(world)
        open(os.path.join(d, "thumb.png"), "wb").write(thumb)
    if real[0] is not None or have_fallback:
        out.append(f'  <image id="zm-height" x="0" y="0" width="{w}" height="{h}" href="/zonemaps/{slug}height.png" preserveAspectRatio="none"/>')
        out.append(f'  <image id="zm-color" x="0" y="0" width="{w}" height="{h}" href="/zonemaps/{slug}color.png" preserveAspectRatio="none" style="display:none"/>')
    else:
        # no layer data anywhere: the procedural placeholder — still write the
        # world.png/thumb.png the world map and the zone cards need
        d = os.path.join(ZONEMAPS, slug)
        os.makedirs(d, exist_ok=True)
        world, thumb = fbm_world_thumb(zone)
        open(os.path.join(d, "world.png"), "wb").write(world)
        open(os.path.join(d, "thumb.png"), "wb").write(thumb)
        b64 = base64.b64encode(heightmap_png(zone)).decode("ascii")
        out.append(f'  <image id="zm-height" x="0" y="0" width="{w}" height="{h}" href="data:image/png;base64,{b64}" opacity="0.55" preserveAspectRatio="none"/>')

    # the generated points of interest are small (r = w/170); enlarge them
    # a bit and give the labels a dark halo so they read on any terrain.
    # The C# generator's ltp-line <line> elements (local in-zone teleports,
    # hidden until map.js lights them up) and the data-ltp tokens on the
    # endpoint column circles pass through untouched.
    last_shape = None  # (kind, markup, radius-growth) waiting for its label
    for kind, at, txt in els:
        if kind == "rect":
            if at.get("fill") == "#10151f":
                continue  # re-emitted above
            if at.get("fill") == "none" and at.get("stroke") == "#54658a":
                continue  # border added by a previous run of this script
            out.append("  " + markup(kind, at))
        elif kind == "line":
            out.append("  " + markup(kind, at))  # grid + ltp lines pass through
        elif kind == "text":
            if last_shape is None:
                out.append("  " + markup("text", at, txt))
                continue
            kind_name, shape_mup, growth = last_shape  # already classified
            # labels sit above their point: lift them by the radius growth
            if not already and growth and "y" in at:
                at["y"] = _fmt(float(at["y"]) - growth)
            if not already and "font-size" in at:
                fs = float(at["font-size"]) * 1.3
                at["font-size"] = _fmt(fs)
                # halo: a wide dark stroke behind the glyphs (paint-order)
                at["paint-order"] = "stroke"
                at["stroke"] = "#070c14"
                at["stroke-opacity"] = "0.85"
                at["stroke-width"] = _fmt(fs * 0.3)
                at["stroke-linejoin"] = "round"
            if at.get("fill") == "#d5dbe8":
                at["fill"] = "#eef4fd"  # brighter over the new backgrounds
            href = resolve(label_names(kind_name, txt), name2slug)
            if href:
                out.append(f'  <a href="{href}" title="{esc(txt)}">')
            out.append("  " + shape_mup)
            out.append("  " + markup("text", at, txt))
            if href:
                out.append("  </a>")
            last_shape = None
        else:
            # circle (column or landing spot) or path (exit gate)
            if last_shape is not None:
                # previous shape had no label — emit it plain
                out.append("  " + last_shape[1])
            shape_kind = "spot" if kind == "circle" and "stroke-dasharray" in at else \
                         ("gate" if kind == "path" else "col")
            growth = 0.0
            if not already and kind == "circle" and "r" in at:
                growth = float(at["r"]) * 0.5
                at["r"] = _fmt(float(at["r"]) + growth)
            if not already and kind in ("circle", "path") and "stroke-width" in at:
                at["stroke-width"] = _fmt(float(at["stroke-width"]) * 1.25)
            last_shape = (shape_kind, markup(kind, at), growth)

    if last_shape is not None:
        out.append("  " + last_shape[1])
    # emphasized zone border on top of the heightmap
    out.append(f'  <rect x="0" y="0" width="{w}" height="{h}" rx="{16 * f}" fill="none" stroke="#54658a" stroke-width="{4 * f}"/>')
    out.append("</svg>")

    with open(path, "w", encoding="utf-8") as fh:
        fh.write("\n".join(out) + "\n")
    return zone


def _rebuild_one(fname, name2slug, slug2id, slug2size):
    """One zone, one process: (fname, zone, links, real, svg-bytes)."""
    path = os.path.join(ZONEMAPS, fname)
    try:
        zone = rebuild(path, name2slug, slug2id, slug2size)
    except Exception as e:
        print(f"  [!] {fname}: {e}")
        zone = None
    new = open(path, encoding="utf-8").read()
    return fname, zone, new.count("<a href="), 'id="zm-color"' in new, len(new.encode("utf-8"))


def main():
    name2slug, slug2id, slug2size = load_zone_meta()
    print(f"name map: {len(name2slug)} zones, {len(slug2id)} with ids")
    print(f"assets: {os.path.abspath(ASSETS)}")
    files = sorted(f for f in os.listdir(ZONEMAPS) if f.endswith(".svg"))
    workers = max(1, min(len(files), os.cpu_count() or 1))
    with Pool(workers) as pool:  # one process per core, one zone each
        results = pool.starmap(_rebuild_one, [(f, name2slug, slug2id, slug2size) for f in files])
    linked = sum(r[2] for r in results)
    real = sum(1 for r in results if r[3])
    total = sum(r[4] for r in results)
    print(f"{len(files)} maps regenerated in parallel ({workers} workers, {real} with real terrain), "
          f"{linked} clickable elements, {total / 1024 / 1024:.2f} MB total")


if __name__ == "__main__":
    main()
