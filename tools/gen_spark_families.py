#!/usr/bin/env python3
"""Regenerate the generated block of content/features/sparks.md (the "Spark
families" overview graph + the per-family card sections) from the card data
already in the page — no database needed. Byte-identical to the C#
generator (SparksPage.cs + SparksFamilySvg.cs): rerunning must not change
the file. The C# generator is the source of truth; run `make generate` to
refresh from the DB, and this tool only for the offline rebuild / CI check.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PAGE = ROOT / "content" / "features" / "sparks.md"
MARKER = "<!-- sparkfamilies:generated -->"
END = '<a id="tree"></a>'

# Family display info (label, slug, color, short box text, long section text)
# — must match SparksPage.Info(). Order = the order the lines appear on the page.
FAM = {
    "special": ("Event & special", "special", "#c8d2e0", "free", "nothing — these are the basic default lines"),
    "tm": ("TM (Truhold-Markson)", "tm", "#41d3ff", "TM standing", "standing with the TM megacorporation (2 → 4 → 6 by level)"),
    "ics": ("ICS", "ics", "#6ee7a0", "ICS standing", "standing with the ICS megacorporation (2 → 4 → 6 by level)"),
    "asi": ("ASI", "asi", "#f5a05a", "ASI standing", "standing with the ASI megacorporation (2 → 4 → 6 by level)"),
    "syndicate": ("Syndicate (NIC)", "syndicate", "#a78bfa", "NIC price", "a NIC price (1M per spark)"),
    "limited": ("Limited", "limited", "#f472b6", "NIC price", "a NIC price (25–50M per spark)"),
}

CARD_RE = re.compile(
    r'<div class="ext-card">\n'
    r'<div class="ext-card-name">(?P<name>.+?)</div>\n'
    r'<div class="ext-card-meta">unlock <span class="(?P<cls>ext-val-\w+)">(?P<unlock>.+?)</span>'
    r' · switch <span class="ext-val-price">(?P<switch>.+?)</span></div>\n'
    r'<div class="ext-card-prereq">(?P<prereq>.+?)</div>\n'
    r'</div>\n',
)
FAM_HEAD_RE = re.compile(r'<a id="family-(?P<slug>\w+)"></a>\n\n### (?P<label>.+?) \((?P<n>\d+)\)\n\n')


def esc(s: str) -> str:
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def build_svg(fams: list) -> str:
    mx, my = 24, 40
    bw, bh, gx, gy, cols = 250, 74, 30, 30, 6
    import math

    rows = math.ceil(len(fams) / cols)
    w = mx * 2 + cols * bw + (cols - 1) * gx
    h = my * 2 + rows * bh + (rows - 1) * gy
    total = sum(f[3] for f in fams)
    out = [
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="1280" height="{int(1280 * h / w)}" role="img" '
        f'aria-label="Spark families: {len(fams)} families, {total} sparks; click a family to jump to its sparks below">',
        f'  <rect x="0" y="0" width="{w}" height="{h}" fill="#10151f" stroke="#39445a" stroke-width="1"/>',
    ]
    for i, (label, slug, color, n, unlock) in enumerate(fams):
        x = mx + (i % cols) * (bw + gx)
        y = my + (i // cols) * (bh + gy)
        tip = f"{label}: {n} sparks, unlocks with {unlock}"
        out.append(
            f'  <g><title>{esc(tip)}</title><a href="#family-{slug}">'
            f'<rect x="{x}" y="{y}" width="{bw}" height="{bh}" rx="10" fill="#10151f" stroke="{color}" stroke-width="1.5"/>'
            f'<text x="{x + bw // 2}" y="{y + 28}" text-anchor="middle" fill="#e8eefc" font-size="15" font-weight="700">{esc(label)}</text>'
            f'<text x="{x + bw // 2}" y="{y + 52}" text-anchor="middle" fill="{color}" font-size="12">{n} sparks · {esc(unlock)}</text>'
            f"</a></g>"
        )
    out.append("</svg>")
    return "\n".join(out) + "\n"


def main() -> int:
    text = PAGE.read_text(encoding="utf-8")
    i = text.index(MARKER) + len(MARKER)
    j = text.index(END, i)
    section = text[i:j]

    # label as it appears in the file (markdown-escaped, e.g. Event &amp; special)
    fams = []  # (label, slug, color, count, box, unlock) in page order; the
    # label comes from the C# source label (unescaped) — the heading itself is
    # re-emitted through the same esc() as C#'s Escape()
    for m in FAM_HEAD_RE.finditer(section):
        slug, n = m.group("slug"), int(m.group("n"))
        if slug not in FAM:
            sys.exit(f"unknown family slug {slug!r} in sparks.md")
        fams.append((FAM[slug][0], slug, FAM[slug][2], n, FAM[slug][3], FAM[slug][4]))

    # sanity: the card counts match the section headings
    for m in FAM_HEAD_RE.finditer(section):
        start = m.end()
        nxt = section.find("<a id=", start)
        block = section[start:nxt if nxt != -1 else len(section)]
        cards = CARD_RE.findall(block)
        if len(cards) != int(m.group("n")):
            sys.exit(f"family {m.group('slug')}: heading says {m.group('n')} sparks, found {len(cards)} cards")

    # rebuild the section exactly as SparksPage.cs writes it
    spark_count = sum(f[3] for f in fams)
    out = [
        "",
        '<a id="families"></a>',
        "",
        "## Spark families",
        "",
        f"The {spark_count} sparks in {len(fams)} families at a glance: one box per family "
        "(spark count, how the line unlocks), left to right in the order the lines were added. "
        "**Click a box to jump to that family's sparks below.** "
        "**Scroll over the diagram to zoom**, drag to pan, and use the \u27f2 button to reset. "
        "The full spark-to-extension detail is the [connection tree](#tree) further down.",
        "",
        '<div class="map-zoom-wrap sparkfam-wrap">',
        '<button type="button" class="zoommap-reset" title="Reset the zoom">\u27f2</button>',
    ]
    # build_svg takes (label, slug, color, count, box) — drop the long unlock text
    _svg = build_svg([(l, s, c, n, b) for (l, s, c, n, b, _u) in fams])
    # strip the trailing newline: the \n-join below supplies the line break
    # before </div> (C# appends the svg string, which ends in </svg>\n)
    # build_svg ends in </svg>\n — drop it, the \n-join supplies the single
    # line break before </div> (C# emits </svg>\n</div>, no blank line)
    out.append(_svg.replace('<svg ', '<svg class="zoommap" ', 1).rstrip("\n"))
    out.append("</div>")
    for k, (label, slug, color, n, box, unlock) in enumerate(fams):
        # re-emit the cards exactly as parsed (they are the source of truth
        # for per-spark detail; only the structure above is regenerated)
        pos = section.index(f'<a id="family-{slug}"')  # from the tag start
        head = FAM_HEAD_RE.search(section, pos)
        start = head.end()
        nxt = section.find("<a id=", start)
        out += ["", f'<a id="family-{slug}"></a>', "", f"### {esc(label)} ({n})", ""]
        out.append(f"Unlock: {unlock}. Each switch costs NIC (the amount is per spark, see the cards) "
                   "and takes a one-hour cooldown — see [Switching sparks](#switching-sparks) below.")
        out.append("")
        out.append('<div class="ext-cards">')
        for m in CARD_RE.finditer(section[start:nxt if nxt != -1 else len(section)]):
            out.append(
                '<div class="ext-card">\n'
                f'<div class="ext-card-name">{m.group("name")}</div>\n'
                f'<div class="ext-card-meta">unlock <span class="{m.group("cls")}">{m.group("unlock")}</span>'
                f' · switch <span class="ext-val-price">{m.group("switch")}</span></div>\n'
                f'<div class="ext-card-prereq">{m.group("prereq")}</div>\n'
                "</div>"
            )
        out.append("</div>")
    out.append("")
    new_section = "\n".join(out) + "\n"

    new_text = text[:i] + new_section + text[j:]
    PAGE.write_text(new_text, encoding="utf-8")
    print(f"sparks.md: {len(fams)} families, {spark_count} sparks ({len(CARD_RE.findall(new_text))} cards)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
