#!/usr/bin/env python3
"""Regenerate the zone teleport map SVGs (static/zonemaps/*.svg):

  * background: the zone's REAL terrain, read from the game's layer files
    (altitude.NNNN.bin, 2 bytes/tile little-endian; see
    content/formats/zone_files.md), sourced in this order:
      1. $OP_ASSETS_DIR/custom-layers/  (default: ../PerpetuumServer2)
      2. the .gbf archives next to it (decoded with the server's own
         script/extract_gbf.py decoder — GXY2 format)
    Two display modes are rendered per zone (128x128 PNGs, referenced by
    the SVG and switchable in the UI by static/zone-map.js):
      height  hillshaded monochrome heightmap (real island shapes)
      color   altitude color ramp + island coastline (blocks Island flag)
    Zones without layer data anywhere (e.g. the training zone) fall back
    to a deterministic procedural heightmap (fBm value noise +
    hillshading, seeded per zone) embedded as base64.
  * an emphasized border around the zone.
  * interactivity: teleport columns, landing spots ("from …") and exit
    gates ("exit → …") whose destination/origin resolves to a zone page
    are wrapped in <a href="/zones/<slug>/"> so they are clickable once the
    SVG is inlined by static/zone-map.js (zoom/pan is then picked up
    automatically by static/map.js).

The input SVGs are produced by generator/Perpetuum.WikiGenerate
(ZoneMapSvg.cs) from the live database. This script only rewrites the
static assets, so it needs no database. Pure standard library (plus the
server's extract_gbf.py, also stdlib, when a .gbf fallback is needed) —
run it locally (`python3 tools/gen_zone_teleport_maps.py`) or via
`make zonemaps` (containerized).

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

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ZONEMAPS = os.path.join(ROOT, "static", "zonemaps")
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
        name2slug.setdefault(name.strip(), slug)
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


def load_terrain(zid, w, h, size):
    """(alt[size][size], coast[size][size] or None) from the game's layers.

    alt: box-averaged elevation. coast: 1 on outline cells of the island
    flag (blocks layer) — the accurate island border. (None, None) when the
    zone has no layer data anywhere.
    """
    id4 = f"{zid:04d}"
    alt_path = os.path.join(ASSETS, "custom-layers", f"altitude.{id4}.bin")
    blk_path = os.path.join(ASSETS, "custom-layers", f"blocks.{id4}.bin")
    alt_raw = blk_raw = None
    if os.path.isfile(alt_path):
        alt_raw = open(alt_path, "rb").read()
        if os.path.isfile(blk_path):
            blk_raw = open(blk_path, "rb").read()
    else:
        alt_raw = _layer_bytes(f"altitude{id4}")
        if blk_raw is None and alt_raw is not None:
            blk_raw = _layer_bytes(f"blocks{id4}")
    if alt_raw is None:
        return None, None
    if len(alt_raw) != w * h * 2:
        print(f"  [!] zone {id4}: altitude layer size {len(alt_raw)} != {w * h * 2} — skipped")
        return None, None

    fx, fy = w // size, h // size
    acc = [[0.0] * w for _ in range(size)]
    for y in range(h):
        row = struct.unpack_from(f"<{w}H", alt_raw, y * w * 2)
        ay = acc[y // fy]
        for x in range(w):
            ay[x] += row[x]
    alt = []
    for oy in range(size):
        ps = [0]
        ps.extend(accumulate(acc[oy]))  # ps[i] = sum of columns [0, i)
        alt.append([(ps[(ox + 1) * fx] - ps[ox * fx]) / (fx * fy) for ox in range(size)])

    coast = None
    if blk_raw is not None and len(blk_raw) == w * h * 2:
        isl = [[0] * size for _ in range(size)]
        for y in range(h):
            base = y * w * 2
            cy = y // fy
            row = isl[cy]
            for x in range(w):
                if blk_raw[base + x * 2] & 8:  # BlockingFlags.Island
                    row[x // fx] = 1
        # outline: island cells touching non-island cells
        coast = [[0] * size for _ in range(size)]
        for y in range(size):
            for x in range(size):
                if not isl[y][x]:
                    continue
                if (x == 0 or y == 0 or x == size - 1 or y == size - 1
                        or not isl[y][x - 1] or not isl[y][x + 1]
                        or not isl[y - 1][x] or not isl[y + 1][x]):
                    coast[y][x] = 1
    return alt, coast


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


def height_png(alt, size=128):
    """Hillshaded monochrome heightmap (real terrain) as PNG bytes."""
    px = bytearray()
    vmax = max(max(r) for r in alt)
    for y in range(size):
        row = alt[y]
        for x in range(size):
            sh = _shade_at(alt, y, x)
            t = (row[x] / vmax) ** 0.65 if vmax else 0.0
            r = int(min(255, (LO[0] + (HI[0] - LO[0]) * t) * sh))
            g = int(min(255, (LO[1] + (HI[1] - LO[1]) * t) * sh))
            b = int(min(255, (LO[2] + (HI[2] - LO[2]) * t) * sh))
            px += bytes((r, g, b, 255))
    return png_encode(size, size, bytes(px))


def color_png(alt, coast, size=128):
    """Altitude color ramp + island coastline as PNG bytes."""
    px = bytearray()
    for y in range(size):
        row = alt[y]
        crow = coast[y] if coast else None
        for x in range(size):
            if crow and crow[x]:
                px += bytes((191, 232, 255, 255))  # island border
                continue
            sh = _shade_at(alt, y, x)
            r, g, b = _ramp(row[x])
            px += bytes((int(min(255, r * sh)), int(min(255, g * sh)), int(min(255, b * sh)), 255))
    return png_encode(size, size, bytes(px))

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


def heightmap_png(zone_name, size=128):
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
    w, h, zone, els = parse(open(path, encoding="utf-8").read())
    f = w / 2048.0
    slug = os.path.basename(path)[:-4] + "/"  # e.g. "zone-asi-a-real/"

    out = []
    out.append(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="640" '
               f'height="{int(640.0 * h / w)}" role="img" aria-label="Teleport map of {esc(zone)}">')
    # zone extent, then the background: real terrain (two switchable modes —
    # static/zone-map.js swaps the images) or, when the zone has no layer
    # data, an embedded procedural heightmap.
    out.append(f'  <rect x="0" y="0" width="{w}" height="{h}" rx="{16 * f}" fill="#10151f" stroke="#39445a" stroke-width="{2 * f}"/>')
    zid = slug2id.get(slug)
    real = (None, None)
    if zid is not None:
        try:
            real = load_terrain(zid, w, h, 128) or (None, None)
        except Exception as e:
            print(f"  [!] {slug}: {e}")
    if real[0] is not None:
        alt, coast = real
        d = os.path.join(ZONEMAPS, slug)
        os.makedirs(d, exist_ok=True)
        open(os.path.join(d, "height.png"), "wb").write(height_png(alt))
        open(os.path.join(d, "color.png"), "wb").write(color_png(alt, coast))
        out.append(f'  <image id="zm-height" x="0" y="0" width="{w}" height="{h}" href="/zonemaps/{slug}height.png" preserveAspectRatio="none"/>')
        out.append(f'  <image id="zm-color" x="0" y="0" width="{w}" height="{h}" href="/zonemaps/{slug}color.png" preserveAspectRatio="none" style="display:none"/>')
    else:
        b64 = base64.b64encode(heightmap_png(zone)).decode("ascii")
        out.append(f'  <image id="zm-height" x="0" y="0" width="{w}" height="{h}" href="data:image/png;base64,{b64}" opacity="0.55" preserveAspectRatio="none"/>')

    last_shape = None  # (kind, markup) waiting for its label
    for kind, at, txt in els:
        if kind == "rect":
            if at.get("fill") == "#10151f":
                continue  # re-emitted above
            if at.get("fill") == "none" and at.get("stroke") == "#54658a":
                continue  # border added by a previous run of this script
            out.append("  " + markup(kind, at))
        elif kind == "line":
            out.append("  " + markup(kind, at))
        elif kind == "text":
            if last_shape is None:
                out.append("  " + markup("text", at, txt))
                continue
            kind_name, shape_mup = last_shape  # already classified
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
            last_shape = (shape_kind, markup(kind, at))

    if last_shape is not None:
        out.append("  " + last_shape[1])
    # emphasized zone border on top of the heightmap
    out.append(f'  <rect x="0" y="0" width="{w}" height="{h}" rx="{16 * f}" fill="none" stroke="#54658a" stroke-width="{4 * f}"/>')
    out.append("</svg>")

    with open(path, "w", encoding="utf-8") as fh:
        fh.write("\n".join(out) + "\n")
    return zone


def main():
    name2slug, slug2id, slug2size = load_zone_meta()
    print(f"name map: {len(name2slug)} zones, {len(slug2id)} with ids")
    print(f"assets: {os.path.abspath(ASSETS)}")
    files = sorted(f for f in os.listdir(ZONEMAPS) if f.endswith(".svg"))
    linked = real = 0
    total = 0
    for fname in files:
        path = os.path.join(ZONEMAPS, fname)
        rebuild(path, name2slug, slug2id, slug2size)
        new = open(path, encoding="utf-8").read()
        linked += new.count("<a href=")
        if 'id="zm-color"' in new:
            real += 1
        total += len(new.encode("utf-8"))
    print(f"{len(files)} maps regenerated ({real} with real terrain), {linked} clickable elements, {total / 1024 / 1024:.2f} MB total")


if __name__ == "__main__":
    main()
