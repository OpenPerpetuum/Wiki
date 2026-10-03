#!/usr/bin/env node
// shot.js — one-shot screenshot helper for the wiki (Chromium via Playwright).
//
// Usage:
//   node tools/shot.js <url> <out.png> [options]
//
// Options:
//   --width <px>      viewport width        (default 1400)
//   --height <px>     viewport height       (default 1000)
//   --scale <n>       deviceScaleFactor     (default 1)
//   --full            full-page screenshot  (default: viewport only)
//   --clip x,y,w,h    clip rectangle in CSS px (implies no --full)
//   --wait <ms>       extra settle time after load (default 400)
//   --idle            wait for networkidle before settling (default: load)
//   --mobile          use a mobile user agent + touch (390x844 unless overridden)
//   --eval <js>       evaluate in the page before the screenshot (can scroll, hover, …)
//
// Examples:
//   node tools/shot.js http://localhost:4199/ /tmp/home.png --full
//   node tools/shot.js http://localhost:4199/zones/map/ /tmp/map.png --width 1280 --height 800
//   node tools/shot.js http://localhost:4199/ /tmp/m.png --mobile
//   node tools/shot.js http://localhost:4199/zones/map/ /tmp/mid.png \
//     --eval "document.querySelector('.home-cards').scrollIntoView()"
//
// The server on the port must already be running (tests/serve.js or whatever).
// Uses CHROMIUM_PATH if set, otherwise the Playwright default.

const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

function die(msg) { console.error(msg); process.exit(1); }

const args = process.argv.slice(2);
if (args.length < 2 || args[0].startsWith('-')) die('usage: shot.js <url> <out.png> [options]');
const [url, out] = args;
const opts = {
  width: 1400, height: 1000, scale: 1, full: false, clip: null,
  wait: 400, idle: false, mobile: false, evalJs: null,
};
for (let i = 2; i < args.length; i++) {
  const a = args[i];
  const next = () => { const v = args[++i] ?? die(`missing value for ${a}`); return v; };
  switch (a) {
    case '--width': opts.width = parseInt(next(), 10); break;
    case '--height': opts.height = parseInt(next(), 10); break;
    case '--scale': opts.scale = parseFloat(next()); break;
    case '--full': opts.full = true; break;
    case '--clip': {
      const [x, y, w, h] = next().split(',').map((s) => parseInt(s, 10));
      if ([x, y, w, h].some(isNaN)) die('--clip expects x,y,w,h');
      opts.clip = { x, y, width: w, height: h };
      opts.full = false;
      break;
    }
    case '--wait': opts.wait = parseInt(next(), 10); break;
    case '--idle': opts.idle = true; break;
    case '--mobile': opts.mobile = true; if (!args[i + 1]?.startsWith('--') && i === 2) { /* keep defaults */ } break;
    case '--eval': opts.evalJs = next(); break;
    default: die(`unknown option ${a}`);
  }
}
if (opts.mobile) { opts.width = opts.width === 1400 ? 390 : opts.width; opts.height = opts.height === 1000 ? 844 : opts.height; }

(async () => {
  const browser = await chromium.launch({
    executablePath: process.env.CHROMIUM_PATH || undefined,
  });
  const ctx = await browser.newContext({
    viewport: { width: opts.width, height: opts.height },
    deviceScaleFactor: opts.scale,
    isMobile: opts.mobile,
    hasTouch: opts.mobile,
    userAgent: opts.mobile
      ? 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1'
      : undefined,
  });
  const page = await ctx.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto(url, { waitUntil: opts.idle ? 'networkidle' : 'load' });
  if (opts.evalJs) await page.evaluate(new Function(opts.evalJs));
  if (opts.wait) await page.waitForTimeout(opts.wait);
  await page.screenshot({ path: out, ...(opts.clip ? { clip: opts.clip } : { fullPage: opts.full }) });
  await browser.close();
  console.log(out);
  if (errors.length) {
    console.error(`page errors:\n${errors.join('\n')}`);
    process.exit(2);
  }
})().catch((e) => die(String(e)));
