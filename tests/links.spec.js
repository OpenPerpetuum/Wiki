// Internal link checker: walks every built HTML page and every static asset,
// collects the internal references (href/src) and fetches each one — broken
// links are the wiki's worst bug class (they only show up when someone
// clicks). Node-side (no browser): the site is a static file tree, so a
// 404 is a 404. The browser-level nav/menu/search tests stay in the spec
// files; this one is the exhaustive sweep.
const { test, expect } = require("@playwright/test");
const fs = require("fs");
const http = require("http");
const path = require("path");

const PUB = path.join(__dirname, "..", "public");

// tests/serve.js (started by playwright.config.js webServer) on this port.
const PORT = process.env.WIKI_TEST_PORT || "4173";
const BASE = `http://127.0.0.1:${PORT}`;

/** All built HTML files, relative paths without leading slash. */
function htmlFiles(dir = PUB, base = "") {
  const out = [];
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    const rel = base ? `${base}/${e.name}` : e.name;
    if (e.isDirectory()) out.push(...htmlFiles(p, rel));
    else if (e.name.endsWith(".html")) out.push(rel);
  }
  return out;
}

/** Internal href/src references in one HTML file: { url -> referring file }.
 * Directory urls keep their trailing slash (the site is trailing-slash; a
 * no-slash url 404s on the static server). */
function refsFrom(html, from, out) {
  const grab = (m) => {
    let u = m.split("#")[0].split("?")[0];
    if (!u.startsWith("/") || u === "/" || u === "") {
      if (u === "/" && !out.has("/")) out.set("/", from);
      return;
    }
    if (!out.has(u)) out.set(u, from);
  };
  for (const m of html.matchAll(/\bhref="([^"]*)"/g)) grab(m[1]);
  for (const m of html.matchAll(/\bsrc="([^"]*)"/g)) grab(m[1]);
}

/** HTTP status of an internal url on the test server (404 when missing). */
function statusOf(url) {
  return new Promise((resolve) => {
    const req = http.get(BASE + url, (res) => {
      res.resume();
      res.on("end", () => resolve(res.statusCode));
    });
    req.on("error", () => resolve(0));
  });
}

test.describe.configure({ mode: "serial" });

test("every internal link and asset reference resolves", async () => {
  const files = htmlFiles();
  expect(files.length, "should have found built pages").toBeGreaterThan(100);

  const refs = new Map();
  for (const rel of files) {
    refsFrom(fs.readFileSync(path.join(PUB, rel), "utf-8"), rel, refs);
  }
  test.info().annotations.push({ type: "refs", description: `${files.length} pages, ${refs.size} unique internal references` });
  expect(refs.size).toBeGreaterThan(100);

  const bad = [];
  let i = 0;
  for (const [url, from] of [...refs.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
    if (++i % 500 === 0) test.info().annotations.push({ type: "progress", description: `checked ${i}/${refs.size}` });
    const code = await statusOf(url);
    if (code !== 200) bad.push(`${from} -> ${url} (${code})`);
  }
  expect(bad, `broken internal links:\n${bad.join("\n")}`).toEqual([]);
});

test("generated zone/map links point at real anchors", async ({ page }) => {
  // The zone pages and the world map cross-link with anchors (#alpha,
  // #family-*, #cat-* …) — verify the targets exist on the target page.
  const bad = [];
  const sample = [
    "/zones/zone-tm/", "/zones/zone-asi/", "/zones/zone-gamma-z106/",
    "/zones/map/", "/features/sparks/", "/content/extensions/",
  ];
  for (const u of sample) {
    const html = fs.readFileSync(path.join(PUB, u.replace(/^\//, "").replace(/\/?$/, "/index.html")), "utf-8");
    for (const m of html.matchAll(/\bhref="([^"]*#[^"]+)"/g)) {
      const h = m[1];
      if (!h.startsWith("/")) continue;
      const mm = h.match(/^\/([^#]*)#(.+)$/);
      if (!mm) continue;
      const targetFile = mm[1] ? mm[1].replace(/\/?$/, "/index.html") : u.replace(/^\//, "").replace(/\/?$/, "/index.html");
      let targetHtml;
      try { targetHtml = fs.readFileSync(path.join(PUB, targetFile), "utf-8"); }
      catch { bad.push(`${u}: target page missing for ${h}`); continue; }
      const id = mm[2].replace(/&amp;/g, "&");
      if (!targetHtml.includes(`id="${id}"`)) bad.push(`${u}: missing anchor ${h}`);
    }
  }
  expect(bad, `broken anchor links:\n${bad.join("\n")}`).toEqual([]);
});
