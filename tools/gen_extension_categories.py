#!/usr/bin/env python3
"""Generate the "main categories" overview of the extension tree.

Reads the committed extensions table (content/content/extensions.md — no
database needed) and:

  * writes static/extensions-categories.svg — one box per extension category
    (extension count, entry points without prerequisites, rank range),
    arranged in three columns by starting rank, with an arrow for every
    cross-category prerequisite (so you can see which categories open up
    which, without the 250-node detail tree)
  * inserts a "## Main categories" section into the extensions page above
    the full tree, embedded with the same zoom/pan wrapper as the world map

The section is marked with <!-- categories:generated --> so re-runs replace
it in place. The long-term source is the .NET generator (ExtensionsPage.cs)
— keep the two in sync.
"""

import html
import os
import re
import sys
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXT = os.path.join(ROOT, "content", "content", "extensions.md")
SVG = os.path.join(ROOT, "static", "extensions-categories.svg")
MARKER = "<!-- categories:generated -->"

PALETTE = ["#41d3ff", "#6ee7a0", "#f5a05a", "#a78bfa", "#f472b6", "#facc15",
           "#38bdf8", "#fb7185", "#4ade80", "#e879f9", "#fdba74", "#93c5fd",
           "#a3e635", "#22d3ee", "#f472b6"]


def esc(s):
    return html.escape(s, quote=True)


def load_table():
    """rows: (name, category, rank, prereqs, state)"""
    rows = []
    for line in open(EXT, encoding="utf-8"):
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if len(cells) >= 10 and cells[0].startswith("ext_"):
            rows.append((cells[0], cells[1], int(cells[2]), cells[8], cells[9]))
    return rows


def main():
    rows = load_table()
    active = [r for r in rows if r[4] in ("active", "hidden")]

    cat = {}  # cat -> dict(count, roots, minr, maxr)
    for _n, c, rank, pre, _st in active:
        d = cat.setdefault(c, {"count": 0, "roots": 0, "minr": 99, "maxr": 0})
        d["count"] += 1
        d["minr"] = min(d["minr"], rank)
        d["maxr"] = max(d["maxr"], rank)
        if pre == "–":
            d["roots"] += 1

    name2cat = {r[0]: r[1] for r in active}
    # cross-category prerequisite edges: requiring cat -> required cat
    edges = defaultdict(list)  # (from_cat, to_cat) -> [(ext, req, lvl)]
    for n, c, _rank, pre, _st in active:
        if pre == "–":
            continue
        for p in pre.split("; "):
            m = re.match(r"(\S+) ≥(\d+)", p)
            if not m:
                continue
            pn, lvl = m.group(1), int(m.group(2))
            if pn in name2cat and name2cat[pn] != c:
                edges[(c, name2cat[pn])].append((n, pn, lvl))

    # columns by starting rank: 1 -> foundation, 2..9 -> advanced, 10 -> spark
    cols = [[], [], []]
    for c, d in sorted(cat.items()):
        cols[0 if d["minr"] <= 1 else 2 if d["minr"] == 10 else 1].append(c)
    col_titles = ["Starting at rank 1", "Building on the core skills", "Spark extensions"]

    # layout
    BW, BH, GX, GY, MX, MY = 250, 74, 70, 26, 24, 46
    ncol = max(len(c) for c in cols)
    W = MX * 2 + 3 * BW + 2 * GX
    H = MY * 2 + ncol * BH + (ncol - 1) * GY
    pos = {}
    for ci, col in enumerate(cols):
        colh = len(col) * BH + (len(col) - 1) * GY
        y0 = MY + (H - 2 * MY - colh) / 2
        for ri, c in enumerate(col):
            pos[c] = (MX + ci * (BW + GX), y0 + ri * (BH + GY))

    s = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="1280" '
         f'height="{int(1280.0 * H / W)}" role="img" '
         f'aria-label="Extension categories: {len(cat)} categories with cross-category prerequisite edges">\n',
         f'  <rect x="0" y="0" width="{W}" height="{H}" fill="#10151f" stroke="#39445a" stroke-width="1"/>\n']
    for ci, t in enumerate(col_titles):
        x = MX + ci * (BW + GX) + BW / 2
        s.append(f'  <text x="{x}" y="{MY - 16}" font-size="13" fill="#8b93a5" text-anchor="middle" '
                 f'font-family="sans-serif">{esc(t)}</text>\n')
    # marker before the edges that reference it
    s.append('  <defs><marker id="arrc" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" '
             'markerHeight="7" orient="auto-start-reverse">'
             '<path d="M 0 0 L 10 5 L 0 10 z" fill="#8b93a5"/></marker></defs>\n')
    # edges (under the boxes): the requiring category points at the required one
    color = {}
    for i, c in enumerate(sorted(cat)):
        color[c] = PALETTE[i % len(PALETTE)]
    for (fc, tc), detail in sorted(edges.items()):
        if fc not in pos or tc not in pos:
            continue
        (fx, fy), (tx, ty) = pos[fc], pos[tc]
        tip = "; ".join(f"{n} requires {pn} ≥{lvl}" for n, pn, lvl in detail)
        if fx == tx:
            # same column: straight vertical line between the boxes
            sx = ex = fx + BW / 2
            if fy < ty:
                sy, ey = fy + BH, ty
                d = f"M {sx} {sy} L {ex} {ey - 2}"
            else:
                sy, ey = fy, ty + BH
                d = f"M {sx} {sy} L {ex} {ey - 2}"
        elif fx < tx:
            sx, sy = fx + BW, fy + BH / 2
            ex, ey = tx, ty + BH / 2
            mx = (sx + ex) / 2
            d = f"M {sx} {sy} C {mx} {sy}, {mx} {ey}, {ex - 2} {ey}"
        else:
            sx, sy = fx, fy + BH / 2
            ex, ey = tx + BW, ty + BH / 2
            mx = (sx + ex) / 2
            d = f"M {sx} {sy} C {mx} {sy}, {mx} {ey}, {ex - 2} {ey}"
        lx, ly = (sx + ex) / 2 + (0 if fx == tx else 0), (sy + ey) / 2
        if fx == tx:
            lx, ly = sx + 12, (sy + ey) / 2
        else:
            ly -= 4
        s.append(f'  <path d="{d}" fill="none" stroke="{color[fc]}" stroke-width="1.4" '
                 f'stroke-opacity="0.55" marker-end="url(#arrc)"><title>{esc(tip)}</title></path>\n')
        s.append(f'  <text x="{lx}" y="{ly}" font-size="11" fill="{color[fc]}" '
                 f'text-anchor="middle" font-family="sans-serif">{len(detail)}×</text>\n')
    # category boxes
    for c in sorted(cat):
        d = cat[c]
        x, y = pos[c]
        label = c[len("extcat_"):].replace("_", " ")
        s.append(f'  <g><title>{esc(label)}: {d["count"]} extensions, {d["roots"]} entry points, '
                 f'rank {d["minr"]}–{d["maxr"]}</title>')
        s.append(f'<rect x="{x}" y="{y}" width="{BW}" height="{BH}" rx="8" fill="#1a2233" '
                 f'stroke="{color[c]}" stroke-width="1.5"/>')
        s.append(f'<text x="{x + 12}" y="{y + 24}" font-size="14" font-weight="bold" fill="#e8ecf4" '
                 f'font-family="sans-serif">{esc(label)}</text>')
        s.append(f'<text x="{x + 12}" y="{y + 46}" font-size="11.5" fill="#aab2c5" font-family="sans-serif">'
                 f'{d["count"]} extensions · {d["roots"]} entry point{"s" if d["roots"] != 1 else ""}</text>')
        s.append(f'<text x="{x + 12}" y="{y + 63}" font-size="11.5" fill="#8b93a5" font-family="sans-serif">'
                 f'rank {d["minr"]}–{d["maxr"]}</text>')
        s.append("</g>\n")
    s.append("</svg>\n")
    open(SVG, "w", encoding="utf-8").write("".join(s))
    print(f"svg: {W}x{H}, {len(cat)} categories, {sum(len(v) for v in edges.values())} cross-category edges")

    # insert the section above the full tree
    txt = open(EXT, encoding="utf-8").read()
    lines = txt.split("\n")
    out, skip = [], False
    for ln in lines:
        if ln.strip() == MARKER:
            skip = True
            continue
        if skip:
            if ln.startswith("<a id=\"tree\"></a>"):
                skip = False
            else:
                continue
        out.append(ln)
    idx = next(i for i, ln in enumerate(out) if ln.startswith("<a id=\"tree\"></a>"))
    section = [
        MARKER,
        "<a id=\"categories\"></a>",
        "",
        "## Main categories",
        "",
        f"The {len(cat)} categories at a glance instead of the {len(active)}-node detail tree: "
        f"one box per category (extension count, entry points without prerequisites, rank range), "
        f"and an arrow for every cross-category prerequisite (hover an arrow for the exact "
        f"requirements) — which categories open up which. **Scroll over the diagram to zoom**, "
        f"drag to pan, and use the ⟲ button to reset.",
        "",
        '<div class="map-zoom-wrap">',
        '<button type="button" class="zoommap-reset" title="Reset the zoom">\u27f2</button>',
        f'<img class="zoommap" src="/extensions-categories.svg" alt="Extension categories: {len(cat)} categories, '
        f'{sum(len(v) for v in edges.values())} cross-category prerequisite edges" loading="lazy">',
        "</div>",
        "",
    ]
    out = out[:idx] + section + out[idx:]
    open(EXT, "w", encoding="utf-8").write("\n".join(out))
    print("extensions page: Main categories section inserted")


if __name__ == "__main__":
    sys.exit(main())
