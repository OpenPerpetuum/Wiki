#!/usr/bin/env node
// gen_world_thumb.js — render static/world-map-thumb.png: the world map
// zoomed onto the alpha (starter) zones, centered, with the beta edge
// visible at the top. The home page's "World Map" card shows this via
// object-fit: cover, so the image is generated at the card's own aspect
// ratio (~4.6:1) — no cropping.
//
// Usage: node tools/gen_world_thumb.js  (server on 4199 must be running)

const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const BASE = process.env.WIKI_BASE || 'http://localhost:4199';
const OUT = path.join(__dirname, '..', 'static', 'world-map-thumb.png');
const CARD_W = 1160, CARD_H = 252; // card aspect ~4.6:1 (1050x230 at 1x)

(async () => {
  const browser = await chromium.launch({
    executablePath: process.env.CHROMIUM_PATH || undefined,
  });
  const page = await (await browser.newContext({
    viewport: { width: 1400, height: 900 },
    deviceScaleFactor: 2,
  })).newPage();
  await page.goto(`${BASE}/zones/map/`, { waitUntil: 'networkidle' });

  const shot = await page.evaluate(async () => {
    const svg = document.querySelector('.zonemap');
    const wrap = document.querySelector('.zonemap-wrap');
    // A short, wide map box = the card's shape.
    wrap.style.height = '252px';
    // No zone names in the thumbnail — just the islands.
    svg.querySelectorAll('text').forEach(t => { t.style.display = 'none'; });
    await new Promise(r => setTimeout(r, 80));

    // Bounding box of the alpha (starter) islands, viewBox units.
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    svg.querySelectorAll('a > g').forEach(g => {
      const t = g.querySelector('title'); if (!t) return;
      if (!/\(alpha\)/.test(t.textContent)) return;
      const el = g.querySelector('rect, circle'); if (!el) return;
      const bb = el.getBBox();
      minX = Math.min(minX, bb.x); minY = Math.min(minY, bb.y);
      maxX = Math.max(maxX, bb.x + bb.width); maxY = Math.max(maxY, bb.y + bb.height);
    });

    // Identity-frame geometry (the verified mapping: screen = (rc - t) + s*P
    // with rc the element-box center, P the identity-frame position).
    const prev = svg.style.transform;
    svg.style.transform = '';
    const r0 = svg.getBoundingClientRect();
    const rcx = r0.left + r0.width / 2, rcy = r0.top + r0.height / 2;
    const m0 = svg.getScreenCTM();
    const pxc = m0.a * (minX + maxX) / 2 + m0.e;
    const pyc = m0.d * (minY + maxY) / 2 + m0.f;
    svg.style.transform = prev;

    const wr = wrap.getBoundingClientRect();
    const u = (r0.height - 32) / 1477; // px per viewBox unit at s = 1
    const s = 1.15 * (r0.width - 32) / ((maxX - minX) * u);
    const tx = (wr.left + wr.width / 2) - rcx - s * (pxc - rcx);
    const ty = (wr.top + wr.height * 0.62) - rcy - s * (pyc - rcy); // alpha slightly low -> beta shows on top
    svg.style.transform = 'translate(' + tx + 'px,' + ty + 'px) scale(' + s + ')';
    await new Promise(r => setTimeout(r, 120));
    const b = wrap.getBoundingClientRect();
    return { x: b.x, y: b.y, w: b.width, h: b.height, s: +s.toFixed(3) };
  });
  await page.screenshot({ path: OUT, clip: { x: shot.x, y: shot.y, width: shot.w, height: shot.h } });
  await browser.close();
  console.log(OUT, 's=' + shot.s, Math.round(shot.w * 2) + 'x' + Math.round(shot.h * 2));
})().catch(e => { console.error(e); process.exit(1); });
