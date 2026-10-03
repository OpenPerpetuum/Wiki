#!/usr/bin/env python3
"""Generate the "main categories" overview of the extension tree.

Reads the committed extensions page (content/content/extensions.md — no
database needed) and:

  * writes static/extensions-categories.svg — one box per extension category
    (extension count, entry points without prerequisites, rank range), the
    columns ordered left to right by starting rank, with an arrow for every
    cross-category prerequisite (so you can see which categories open up
    which). Each box is a link to its category section on the extensions
    page. The spark-extension category sits outside the diagram (no
    prerequisites of its own) — it has its own diagram on the sparks page
    (generator: SparksTree.cs).
  * embeds that SVG inline in the "## Main categories" section of the
    extensions page (inline so the boxes are real links), with the same
    zoom/pan wrapper as the world map, above the per-category cards

The section is marked with <!-- categories:generated --> so re-runs replace
it in place. The long-term source is the .NET generator
(ExtensionsPage.cs / ExtensionsCategories.cs) — this tool must produce
byte-identical output.
"""

import html
import os
import re
import sys
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXT = os.path.join(ROOT, "content", "content", "extensions.md")
SVG = os.path.join(ROOT, "static", "extensions-categories.svg")
MARKER = "<!-- categories:generated -->"

PALETTE = ["#41d3ff", "#6ee7a0", "#f5a05a", "#a78bfa", "#f472b6", "#facc15",
           "#38bdf8", "#fb7185", "#4ade80", "#e879f9", "#fdba74", "#93c5fd",
           "#a3e635", "#22d3ee", "#f472b6"]


def esc(s):
    return html.escape(s, quote=True)


def fmt(v):
    # like the C# int formatting: no trailing .0
    return str(int(v)) if float(v).is_integer() else str(v)


def load_cards():
    """Parse the per-category card sections.
    Returns (cards, total): cards = list of (category, name, rank, prereq, active)."""
    cards = []
    cat = None
    for line in open(EXT, encoding="utf-8"):
        m = re.match(r"^### (.+) \((\d+)\)$", line.strip())
        if m:
            cat = m.group(1)  # a per-category heading opens the card region
            continue
        if line.startswith("# ") or line.startswith("## "):
            cat = None  # any other heading ends it
            continue
        if cat is None:
            continue
        if "<div class=\"ext-card-name\">" in line:
            name = re.search(r">(.+)</div>", line).group(1)
            cards.append([cat, html.unescape(name), 0, "", True])
            continue
        if "<div class=\"ext-card-meta\">" in line:
            meta = re.search(r">(.+)</div>", line).group(1)
            rm = re.search(r"rank (\d+)", meta)
            cards[-1][2] = int(rm.group(1)) if rm else 0
            cards[-1][4] = "inactive" not in meta
            continue
        if "<div class=\"ext-card-prereq\">" in line:
            p = re.search(r">(.+)</div>", line).group(1)
            cards[-1][3] = html.unescape(p)
    total = sum(1 for ln in open(EXT, encoding="utf-8") if "<div class=\"ext-card\">" in ln)
    return cards, total


def slug_cat(display_name):
    # Must stay identical to ExtensionsPage.SlugCat (C#): "extcat_craft" ->
    # "craft"; lowercased, non-alphanumerics to '-', collapsed, trimmed.
    s = re.sub(r"[^a-z0-9]+", "-", display_name.lower()).strip("-")
    return re.sub(r"-{2,}", "-", s)


def main():
    cards, total = load_cards()
    active = [c for c in cards if c[4]]
    if not active:
        print("error: no extension cards found in extensions.md (layout changed?)")
        return 1

    cat = {}  # cat -> dict(count, roots, minr, maxr)
    for c, _n, rank, _pre, _a in active:
        d = cat.setdefault(c, {"count": 0, "roots": 0, "minr": 99, "maxr": 0})
        d["count"] += 1
        d["minr"] = min(d["minr"], rank)
        d["maxr"] = max(d["maxr"], rank)
        if _pre == "no prerequisites":
            d["roots"] += 1

    # display name -> category (for resolving prerequisite edges)
    name2cat = {}
    for c, n, _r, _p, _a in active:
        name2cat.setdefault(n, c)
    edges = defaultdict(list)  # (from_cat, to_cat) -> [(ext, req, lvl)]
    for c, n, _rank, pre, _a in active:
        if pre == "no prerequisites":
            continue
        for p in pre.split("; "):
            m = re.match(r"(.+) ≥(\d+)$", p.strip())
            if not m:
                continue
            pn, lvl = m.group(1), int(m.group(2))
            if pn in name2cat and name2cat[pn] != c:
                edges[(c, name2cat[pn])].append((n, pn, lvl))
    # C# orders tooltips by (required, requiring) display name — match it.
    for k in edges:
        edges[k].sort(key=lambda t: (t[1], t[0]))

    # The spark category has its own diagram (sparks page): excluded here.
    spark_cats = {c for c in cat if c.lower().startswith("spark")}
    laid = [c for c in cat if c not in spark_cats]
    # Columns ordered left to right by the starting rank.
    cols = [
        sorted((c for c in laid if cat[c]["minr"] <= 1), key=lambda c: (cat[c]["minr"], c)),
        sorted((c for c in laid if 2 <= cat[c]["minr"] <= 3), key=lambda c: (cat[c]["minr"], c)),
        sorted((c for c in laid if 4 <= cat[c]["minr"] < 10), key=lambda c: (cat[c]["minr"], c)),
    ]
    col_titles = ["Starting at rank 1", "Building on rank 2", "Building on rank 4+"]

    # layout
    BW, BH, GX, GY, MX, MY = 250, 74, 70, 26, 24, 46
    ncol = max(1, max(len(c) for c in cols))
    W = MX * 2 + 3 * BW + 2 * GX
    H = MY * 2 + ncol * BH + (ncol - 1) * GY
    pos = {}
    for ci, col in enumerate(cols):
        colh = len(col) * BH + (len(col) - 1) * GY
        y0 = MY + (H - 2 * MY - colh) / 2
        for ri, c in enumerate(col):
            pos[c] = (MX + ci * (BW + GX), y0 + ri * (BH + GY))

    color = {}
    for i, c in enumerate(sorted(cat)):
        color[c] = PALETTE[i % len(PALETTE)]

    s = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="1280" '
         f'height="{int(1280.0 * H / W)}" role="img" '
         f'aria-label="Extension categories: {len(cat)} categories with cross-category prerequisite edges">\n',
         f'  <rect x="0" y="0" width="{W}" height="{H}" fill="#10151f" stroke="#39445a" stroke-width="1"/>\n']
    for ci, t in enumerate(col_titles):
        x = MX + ci * (BW + GX) + BW / 2
        s.append(f'  <text x="{fmt(x)}" y="{MY - 16}" font-size="13" fill="#8b93a5" text-anchor="middle" '
                 f'font-family="sans-serif">{esc(t)}</text>\n')
    s.append('  <defs><marker id="arrc" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" '
             'markerHeight="7" orient="auto-start-reverse">'
             '<path d="M 0 0 L 10 5 L 0 10 z" fill="#8b93a5"/></marker></defs>\n')
    # edges (under the boxes): the requiring category points at the required one
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
                d = f"M {fmt(sx)} {fmt(sy)} L {fmt(ex)} {fmt(ey - 2)}"
            else:
                sy, ey = fy, ty + BH
                d = f"M {fmt(sx)} {fmt(sy)} L {fmt(ex)} {fmt(ey - 2)}"
        elif fx < tx:
            sx, sy = fx + BW, fy + BH / 2
            ex, ey = tx, ty + BH / 2
            mx = (sx + ex) / 2
            d = f"M {fmt(sx)} {fmt(sy)} C {fmt(mx)} {fmt(sy)}, {fmt(mx)} {fmt(ey)}, {fmt(ex - 2)} {fmt(ey)}"
        else:
            sx, sy = fx, fy + BH / 2
            ex, ey = tx + BW, ty + BH / 2
            mx = (sx + ex) / 2
            d = f"M {fmt(sx)} {fmt(sy)} C {fmt(mx)} {fmt(sy)}, {fmt(mx)} {fmt(ey)}, {fmt(ex - 2)} {fmt(ey)}"
        lx, ly = (sx + ex) / 2, (sy + ey) / 2
        if fx == tx:
            lx, ly = sx + 12, (sy + ey) / 2
        else:
            ly -= 4
        s.append(f'  <path d="{d}" fill="none" stroke="{color[fc]}" stroke-width="1.4" '
                 f'stroke-opacity="0.55" marker-end="url(#arrc)"><title>{esc(tip)}</title></path>\n')
        s.append(f'  <text x="{fmt(lx)}" y="{fmt(ly)}" font-size="11" fill="{color[fc]}" '
                 f'text-anchor="middle" font-family="sans-serif">{len(detail)}×</text>\n')
    # category boxes (only the laid-out ones) — each a link to its section
    # on the extensions page (the SVG is embedded inline there, so the
    # href="#cat-..." anchor works)
    for c in sorted(pos):
        d = cat[c]
        x, y = pos[c]
        label = c.replace(" ", " ")
        anchor = "#cat-" + slug_cat(label)
        s.append(f'  <g><title>{esc(label)}: {d["count"]} extensions, {d["roots"]} entry points, '
                 f'rank {d["minr"]}–{d["maxr"]}</title><a href="{anchor}">')
        s.append(f'<rect x="{fmt(x)}" y="{fmt(y)}" width="{BW}" height="{BH}" rx="8" fill="#1a2233" '
                 f'stroke="{color[c]}" stroke-width="1.5"/>')
        s.append(f'<text x="{x + 12}" y="{fmt(y + 24)}" font-size="14" font-weight="bold" fill="#e8ecf4" '
                 f'font-family="sans-serif">{esc(label)}</text>')
        s.append(f'<text x="{x + 12}" y="{fmt(y + 46)}" font-size="11.5" fill="#aab2c5" font-family="sans-serif">'
                 f'{d["count"]} extensions · {d["roots"]} entry point{"s" if d["roots"] != 1 else ""}</text>')
        s.append(f'<text x="{x + 12}" y="{fmt(y + 63)}" font-size="11.5" fill="#8b93a5" font-family="sans-serif">'
                 f'rank {d["minr"]}–{d["maxr"]}</text>')
        s.append("</a></g>\n")
    s.append("</svg>\n")
    open(SVG, "w", encoding="utf-8").write("".join(s))
    print(f"svg: {W}x{H}, {len(cat)} categories, {sum(len(v) for v in edges.values())} cross-category edges")

    # embed/replace the section above the per-category cards (the C# generator
    # writes the identical bytes — keep both in sync)
    txt = open(EXT, encoding="utf-8").read()
    lines = txt.split("\n")
    out, skip = [], False
    for ln in lines:
        if ln.strip() == MARKER:
            skip = True
            continue
        if skip:
            if ln.startswith("<a id=\"table\"></a>"):
                skip = False
            else:
                continue
        out.append(ln)
    idx = next(i for i, ln in enumerate(out) if ln.startswith("<a id=\"table\"></a>"))
    # inline SVG (not <img>) so the boxes' <a href="#cat-..."> links work;
    # rstrip the trailing newline — joining the section adds its own
    inline = "".join(s).replace('<svg ', '<svg class="zoommap" ', 1).rstrip("\n")
    section = [
        MARKER,
        "<a id=\"categories\"></a>",
        "",
        "## Main categories",
        "",
        f"The {len(cat)} categories at a glance: one box per category (extension count, "
        "entry points without prerequisites, rank range), left to right by starting rank, and an "
        "arrow for every cross-category prerequisite (hover an arrow for the exact requirements) — "
        "which categories open up which. **Click a box to jump to that category's cards below.** "
        "The spark extensions sit outside this diagram (no prerequisites of their own) — their "
        "bundle diagram is on the [Sparks](/features/sparks/) page. **Scroll over the diagram to "
        "zoom**, drag to pan, and use the ⟲ button to reset.",
        "",
        '<div class="map-zoom-wrap extcats-wrap">',
        '<button type="button" class="zoommap-reset" title="Reset the zoom">\u27f2</button>',
        inline,
        "</div>",
        "",
    ]
    out = out[:idx] + section + out[idx:]
    open(EXT, "w", encoding="utf-8").write("\n".join(out))
    print("extensions page: Main categories section embedded")


if __name__ == "__main__":
    sys.exit(main())
