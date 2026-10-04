#!/usr/bin/env python3
"""Regenerates wiki/static/search_index.json.

Replicates generator/Perpetuum.WikiGenerate/SearchIndex.cs (Build method):
scans every markdown page under content/, reads the front-matter title and
description, computes the Zola URL path, and writes a compact JSON array of
{"t","d","u"} objects with .NET's default (JavaScriptEncoder.Default) escaping.
Used only when the .NET generator is not runnable locally.
"""
import json
import sys
from pathlib import Path

root = Path(sys.argv[1] if len(sys.argv) > 1 else ".")
content_root = root / "content"

def front_matter(text):
    if not text.startswith("---\n"):
        return ("", "")
    end = text.find("\n---", 4)
    if end < 0:
        return ("", "")
    title, description = "", ""
    for line in text[4:end].split("\n"):
        if line.startswith("title:"):
            title = unquote(line[len("title:"):].strip())
        elif line.startswith("description:"):
            description = unquote(line[len("description:"):].strip())
    return (title, description)

def unquote(s):
    return s[1:-1] if len(s) >= 2 and s.startswith('"') and s.endswith('"') else s

entries = []
for f in sorted(content_root.rglob("*.md"), key=lambda p: str(p.relative_to(content_root))):
    rel = f.relative_to(content_root)
    parts = list(rel.parts)
    file_name = parts[-1]
    is_section = file_name in ("_index.md", "index.md")
    url_parts = [p.replace("_", "-") for p in parts[:-1]]
    if not is_section:
        url_parts.append(f.stem.replace("_", "-"))
    title, description = front_matter(f.read_text())
    if not title:
        continue
    url = "/" + "/".join(url_parts) + "/" if url_parts else "/"
    entries.append({"t": title, "d": description, "u": url})

s = json.dumps(entries, separators=(",", ":"), ensure_ascii=True)
# .NET JavaScriptEncoder.Default also escapes these (json.dumps does not).
for ch, esc in (("&", "\\u0026"), ("<", "\\u003c"), (">", "\\u003e"),
                ("'", "\\u0027"), ("+", "\\u002b")):
    s = s.replace(ch, esc)

out = content_root.parent / "static" / "search_index.json"
out.write_text(s + "\n")
print(f"wrote {out} ({len(entries)} pages)")
