#!/usr/bin/env python3
"""Regenerate the zone teleport map SVGs (static/zonemaps/*.svg):

  * background: a deterministic procedural heightmap (fBm value noise +
    hillshading, seeded per zone) over the zone's extent, and an emphasized
    border around the zone. Real game terrain textures are NOT used — they
    are proprietary game assets; this approximation keeps the open-source
    wiki clean.
  * interactivity: teleport columns, landing spots ("from …") and exit
    gates ("exit → …") whose destination/origin resolves to a zone page
    are wrapped in <a href="/zones/<slug>/"> so they are clickable once the
    SVG is inlined by static/zone-map.js (zoom/pan is then picked up
    automatically by static/map.js).

The input SVGs are produced by generator/Perpetuum.WikiGenerate
(ZoneMapSvg.cs) from the live database. This script only rewrites the
static assets, so it needs no database. Pure standard library — run it
locally (`python3 tools/gen_zone_teleport_maps.py`) or via `make zonemaps`
(containerized).

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

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ZONEMAPS = os.path.join(ROOT, "static", "zonemaps")
ZONE_INDEX = os.path.join(ROOT, "content", "zones", "zone-index.md")

# ---------------------------------------------------------------- name map

def load_name_map():
    """Display zone name -> /zones/<slug>/ from the zone index table."""
    text = open(ZONE_INDEX, encoding="utf-8").read()
    rows = re.findall(r"\|\s*\d+\s*\|\s*\[([^\]]+)\]\((/zones/[^)]+)\)", text)
    name2slug = {}
    for name, slug in rows:
        name2slug.setdefault(name.strip(), slug)
    return name2slug


def resolve(names, name2slug):
    """First label part that resolves to a zone page; None if unresolvable."""
    for n in names:
        n = n.strip().rstrip("…").strip()
        if n and n in name2slug:
            return name2slug[n]
    return None

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


def rebuild(path, name2slug):
    w, h, zone, els = parse(open(path, encoding="utf-8").read())
    f = w / 2048.0
    b64 = base64.b64encode(heightmap_png(zone)).decode("ascii")

    out = []
    out.append(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="640" '
               f'height="{int(640.0 * h / w)}" role="img" aria-label="Teleport map of {esc(zone)}">')
    # zone extent + generated heightmap (approximate terrain — game texture
    # assets are not redistributed), grid kept from the generated file,
    # emphasized border on top.
    out.append(f'  <rect x="0" y="0" width="{w}" height="{h}" rx="{16 * f}" fill="#10151f" stroke="#39445a" stroke-width="{2 * f}"/>')
    out.append(f'  <image x="0" y="0" width="{w}" height="{h}" href="data:image/png;base64,{b64}" opacity="0.55" preserveAspectRatio="none"/>')

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
    name2slug = load_name_map()
    print(f"name map: {len(name2slug)} zones")
    files = sorted(f for f in os.listdir(ZONEMAPS) if f.endswith(".svg"))
    linked = 0
    total = 0
    for fname in files:
        path = os.path.join(ZONEMAPS, fname)
        rebuild(path, name2slug)
        new = open(path, encoding="utf-8").read()
        linked += new.count("<a href=")
        total += len(new.encode("utf-8"))
    print(f"{len(files)} maps regenerated, {linked} clickable elements, {total / 1024 / 1024:.2f} MB total")


if __name__ == "__main__":
    main()
