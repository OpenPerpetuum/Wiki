#!/usr/bin/env python3
"""Prefix site-absolute links with the deploy base path (CI deploy step).

The site is written against the domain root (base_url = "/") so local
development and the test job see the same output. GitHub Pages serves a
PROJECT site under a subpath (openperpetuum.github.io/wiki/), and Zola 0.23
does NOT rewrite site-absolute links ("/style.css", "/zones/...") — only
the sitemap's canonical URLs pick up base_url. This pass prefixes every
href="/..." and src="/..." in the built HTML/SVG with the base's path
part ("/Wiki") so the same build works under the subpath.

Usage:  python3 tools/rewrite_site_base.py <site_dir> <base_url>
        python3 tools/rewrite_site_base.py public https://openperpetuum.github.io/wiki

Idempotent: values already carrying the path part are left alone. Protocol
links ("//cdn..."), data: URIs, hashes and relative links never match.
"""
import os
import re
import sys
from urllib.parse import urlparse

# href="..." / src="..." whose value is site-absolute ("/..." but not "//...")
ATTR = re.compile(r'((?:href|src)\s*=\s*")(/(?!/)[^"]*)(")')


def main() -> None:
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    site, base = sys.argv[1], sys.argv[2]
    path = urlparse(base).path.rstrip("/")
    if not path:
        print(f"base {base} has no path part; nothing to rewrite")
        return

    def rep(m: "re.Match[str]") -> str:
        # m.group(2) starts with "/" and (by the regex) not "//"
        v = m.group(2)
        if v.startswith(path + "/") or v == path:
            return m.group(0)  # already prefixed
        return m.group(1) + path + v + m.group(3)

    count = 0
    for root, _dirs, files in os.walk(site):
        for f in files:
            if not (f.endswith(".html") or f.endswith(".svg")):
                continue
            p = os.path.join(root, f)
            with open(p, encoding="utf-8") as fh:
                text = fh.read()
            new = ATTR.sub(rep, text)
            if new != text:
                count += 1
                with open(p, "w", encoding="utf-8") as fh:
                    fh.write(new)
    print(f"prefixed links in {count} files with base path {path}/")


if __name__ == "__main__":
    main()
