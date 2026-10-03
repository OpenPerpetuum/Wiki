#!/usr/bin/env python3
"""Add "what can be produced with this" sections to the wiki item pages.

Reads the committed generated pages (no database needed):

  * content/content/recipes.md  — the component table (the recipe data)
  * content/content/items/*.md  — the item registry (definition, display
    name, category, URL)
  * content/content/ores/*.md   — ore pages (slug == definition minus def_)

and then:

  1. writes tools/recipes_data.json (the parsed recipe table + registry) —
     consumed by tools/gen_recipes_cards.py
  2. inserts the production sections into every item page that can be
     produced or used in production:
       "## Production"          — a mermaid tree of the components (left,
                                 with required amounts) that build this item
                                 (right, in green), when it is a recipe
                                 product
       "## Used in production"  — a mermaid tree of what can be produced
                                 with it (capped at 8 products + a "+N more"
                                 node into the recipes page for high-fan-out
                                 materials), each product linked to its page

Ore pages already carry a complete "Made from it" table from the generator
(OresPage.cs), so they are left alone.

The section is marked with <!-- production:generated --> so re-runs replace
it in place instead of duplicating it. The long-term source of this content
is the .NET generator (ItemsPage.cs / OresPage.cs) — keep the two in sync.
"""

import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ITEMS = os.path.join(ROOT, "content", "content", "items")
ORES = os.path.join(ROOT, "content", "content", "ores")  # registry only (component links)
RECIPES = os.path.join(ROOT, "content", "content", "recipes.md")
DATA = os.path.join(ROOT, "tools", "recipes_data.json")
MARKER = "<!-- production:generated -->"
MAX_PRODUCTS = 8  # mermaid nodes shown before the "+N more" node


def slug_of(definition: str) -> str:
    # mirrors ItemsPage.Slug: strip def_, lowercase, dashes
    return definition[len("def_"):].lower().replace("_", "-")


def esc(s: str) -> str:
    return s.replace('"', "'")


def derived_name(definition: str) -> str:
    """Fallback display name (mirrors the generator's derived naming)."""
    words = definition[len("def_"):].split("_")
    return " ".join(w.capitalize() if w.isalpha() else w for w in words)


def product_info(definition, registry, ores, names):
    """(display name, url or None) for a produced item — bots and hidden
    items have no item page; bot definitions link to the robot overview.
    Display names come from the generator's names map (client strings),
    falling back to the derived name."""
    if definition in registry:
        return registry[definition][0], registry[definition][2]
    if definition in ores:
        return ores[definition][0], ores[definition][1]
    if definition.endswith("_bot"):
        return names.get(definition, derived_name(definition)), "/content/robots/"
    return names.get(definition, derived_name(definition)), None


def load_registry():
    """definition -> (display name, category, url)."""
    reg = {}
    for f in sorted(os.listdir(ITEMS)):
        if not f.endswith(".md") or f == "_index.md":
            continue
        txt = open(os.path.join(ITEMS, f), encoding="utf-8").read()
        m = re.search(r"\| Definition \| `(\w+)`", txt)
        if not m:
            continue
        t = re.search(r'^title: "(.*)"', txt, re.M)
        c = re.search(r"\| Category \| (.+?) \|", txt)
        reg[m.group(1)] = (
            t.group(1) if t else m.group(1),
            c.group(1) if c else "?",
            "/content/items/" + slug_of(m.group(1)) + "/",
        )
    ores = {}
    for f in sorted(os.listdir(ORES)):
        if not f.endswith(".md") or f == "_index.md":
            continue
        txt = open(os.path.join(ORES, f), encoding="utf-8").read()
        t = re.search(r'^title: "(.*)"', txt, re.M)
        d = "def_" + f[:-3]
        ores[d] = (t.group(1) if t else d, "/content/ores/" + f[:-3] + "/")
    return reg, ores


def load_recipes():
    """definition -> ([(component definition, qty), ...], research level str).
    Parses the committed table; when the table is gone (the card layout has
    replaced it) falls back to the data cache. Also returns the display-name
    map the .NET generator wrote (client strings for definitions that have
    no page of their own)."""
    has_table = any(
        line.startswith("| def_")
        for line in open(RECIPES, encoding="utf-8"))
    if not has_table and os.path.isfile(DATA):
        data = json.load(open(DATA, encoding="utf-8"))
        if data.get("recipes"):
            return ({k: ([(c, q) for c, q in v["components"]], v["research"])
                     for k, v in data["recipes"].items()},
                    data.get("names", {}))
    recipes = {}
    for line in open(RECIPES, encoding="utf-8"):
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if len(cells) >= 3 and cells[0].startswith("def_"):
            comps = []
            for part in cells[1].split(", "):
                m = re.match(r"(\S+) ×(\d+)", part)
                if m:
                    comps.append((m.group(1), int(m.group(2))))
            recipes[cells[0]] = (comps, cells[2])
    return recipes, {}


def num(n: int) -> str:
    if n >= 1_000_000:
        return f"{n / 1_000_000:.1f}M"
    if n >= 1_000:
        return f"{n / 1_000:.1f}k"
    return str(n)


def recipe_section(name: str, comps, research, registry, ores, names):
    """Mermaid tree: the components (left, with amounts) -> this item (right, green)."""
    rl = int(research) if str(research).isdigit() else 0
    intro = (f"**Produced from {num(len(comps))} component{'s' if len(comps) != 1 else ''}"
             + (f", research level {rl}" if rl > 0 else "")
             + "** — assemble the components to build it (see [Recipes](/content/recipes/) for the full list):\n\n")
    lines = ["```mermaid", "graph LR"]
    lines.append(f'    a["{esc(name)}"]:::current')
    for i, (comp, amt) in enumerate(comps):
        letter = chr(ord("b") + i)
        cname, url = product_info(comp, registry, ores, names)
        lines.append(f'    {letter}["{esc(cname)} ×{num(amt)}"]:::comp')
        lines.append(f"    {letter} --> a")
        if url:
            lines.append(f'    click {letter} "{url}" "{esc(cname)}"')
    lines.append("    classDef current fill:#2f9e6f,stroke:#1f6f4a,color:#ffffff")
    lines.append("    classDef comp fill:#3b6ea5,stroke:#274a75,color:#ffffff")
    lines.append("```")
    return intro + "\n".join(lines)


def production_section(name: str, users, registry, ores, names):
    """Mermaid tree: this item -> what can be produced with it."""
    # Stable order; the capped sample prefers products with their own page
    # over internal definitions (robot parts, bot fits) that only exist in
    # the recipe table.
    with_page = sorted(u for u in users if u in registry or u in ores)
    rest = sorted(u for u in users if u not in registry and u not in ores)
    users = with_page + rest
    shown = users[:MAX_PRODUCTS]
    extra = len(users) - len(shown)
    lines = ["```mermaid", "graph LR"]
    lines.append(f'    a["{esc(name)}"]:::current')
    for i, u in enumerate(shown):
        letter = chr(ord("b") + i)
        uname, url = product_info(u, registry, ores, names)
        lines.append(f'    {letter}["{esc(uname)}"]:::prod')
        lines.append(f"    a --> {letter}")
        if url:
            lines.append(f'    click {letter} "{url}" "{esc(uname)}"')
    if extra > 0:
        m = chr(ord("b") + len(shown))
        lines.append(f'    {m}["+{num(extra)} more"]:::more')
        lines.append(f"    a --> {m}")
        lines.append(f'    click {m} "/content/recipes/" "All recipes"')
    lines.append("    classDef current fill:#2f9e6f,stroke:#1f6f4a,color:#ffffff")
    lines.append("    classDef prod fill:#3b6ea5,stroke:#274a75,color:#ffffff")
    lines.append("    classDef more fill:#39445a,stroke:#54658a,color:#d5dbe8")
    lines.append("```")
    return "\n".join(lines)


def insert_section(path, section, before_line):
    """Replace an existing marked section or insert before `before_line`."""
    lines = open(path, encoding="utf-8").read().split("\n")
    # Drop a previous section: the section starts at the marker and always
    # ends right before `before_line`, so everything in between is ours.
    out, skip = [], False
    for ln in lines:
        if ln.strip() == MARKER:
            skip = True
            continue
        if skip:
            if ln.startswith(before_line):
                skip = False
            else:
                continue
        out.append(ln)
    idx = max(i for i, ln in enumerate(out) if ln.startswith(before_line))
    out = out[:idx] + section.rstrip("\n").split("\n") + [""] + out[idx:]
    open(path, "w", encoding="utf-8").write("\n".join(out))


def intro_text(n: int, sample: bool) -> str:
    if sample:
        return (f"**Component of {num(n)} items** — a sample of what can be "
                f"produced with it (the full list is in [Recipes](/content/recipes/)).\n\n")
    return f"**Component of {num(n)} items** — everything that uses it in production:\n\n"


def item_section(name, comps, research, users, registry, ores, names):
    """The marked production area: the recipe tree (when this item is a recipe
    product) and the end-products tree (when it is a component of others) —
    same sections, same order, as ItemsPage.cs."""
    parts = []
    if comps:
        parts.append("## Production\n\n" + recipe_section(name, comps, research, registry, ores, names))
    if users:
        parts.append("## Used in production\n\n"
                     + intro_text(len(users), len(users) > MAX_PRODUCTS)
                     + production_section(name, users, registry, ores, names))
    if not parts:
        return None
    return MARKER + "\n" + "\n".join(parts) + "\n"


def main():
    registry, ores = load_registry()
    recipes, names = load_recipes()
    json.dump(
        {
            "recipes": {k: {"components": [[c, q] for c, q in v[0]], "research": v[1]}
                        for k, v in recipes.items()},
            "names": names,
            "items": {k: {"name": v[0], "category": v[1], "url": v[2]} for k, v in registry.items()},
            "ores": {k: {"name": v[0], "url": v[1]} for k, v in ores.items()},
        },
        open(DATA, "w", encoding="utf-8"), indent=1, ensure_ascii=False,
    )
    print(f"registry: {len(registry)} items, {len(ores)} ores, {len(recipes)} recipes -> {DATA}")

    # reverse map: component definition -> list of (item definition)
    users = {}
    for item, (comps, _rl) in recipes.items():
        for comp, _q in comps:
            users.setdefault(comp, set()).add(item)

    done = 0
    for item_def in sorted(set(users) | set(recipes)):
        if item_def not in registry:
            continue  # ores keep their "Made from it" table; bots/parts have no page
        if item_def.endswith("_CT_capsule"):
            continue  # capsule pages already carry a payload "Production" table
        fname = item_def[len("def_"):] + ".md"
        path = os.path.join(ITEMS, fname)
        if not os.path.isfile(path):
            continue
        name, _cat, _url = registry[item_def]
        comps, research = recipes.get(item_def, ([], "–"))
        who = sorted(users.get(item_def, ()))
        section = item_section(name, comps, research, who, registry, ores, names)
        if section is None:
            continue
        insert_section(path, section, "[All items]")
        done += 1
    print(f"production sections: {done} pages updated")


if __name__ == "__main__":
    sys.exit(main())
