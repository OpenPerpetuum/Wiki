// Zone teleport maps: inlined at runtime from /zonemaps/*.svg (zone-map.js)
// and made interactive by map.js — wheel zoom, pan, reset — with their
// teleport elements clickable, linking to the zone pages.
const { test, expect } = require('@playwright/test');

// desktop-only: these drive the mouse over fixed desktop map geometry
test.beforeEach(async ({ page }) => {
  test.skip(page.viewportSize().width <= 500, 'desktop layout only');
});

// Hokkogaros — 10 labelled teleport columns
const ZONE = '/zones/zone-asi-a-real/';

test('zone map is inlined with its terrain background', async ({ page, baseURL }) => {
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto(`${baseURL}${ZONE}`, { waitUntil: 'networkidle' });
  const wrap = page.locator('.zonetp-wrap');
  await wrap.waitFor({ state: 'visible', timeout: 10000 });
  const svg = wrap.locator('svg.zonemap');
  expect(await svg.count()).toBe(1);
  expect(await page.locator('img[src^="/zonemaps/"]').count()).toBe(0); // img replaced
  // this zone has real terrain: two switchable mode images
  const bg = svg.locator('image');
  expect(await bg.count()).toBe(2);
  const href = await bg.first().getAttribute('href');
  expect(href).toMatch(/^\/zonemaps\//);
  const r = await page.request.get(href); // the terrain PNG is served
  expect(r.ok()).toBe(true);
  expect(errors).toEqual([]);
});

test('display modes: height and color switch the background', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${ZONE}`, { waitUntil: 'networkidle' });
  const wrap = page.locator('.zonetp-wrap');
  await wrap.waitFor({ state: 'visible', timeout: 10000 });
  // exactly two modes
  expect(await wrap.locator('.zonemap-mode').count()).toBe(2);
  const visible = (sel) => page.evaluate((s) => {
    const el = document.querySelector(s);
    return el && el.style.display !== 'none';
  }, sel);
  expect(await visible('image#zm-height')).toBe(true);
  expect(await visible('image#zm-color')).toBe(false);

  await wrap.locator('.zonemap-mode[data-mode="color"]').click();
  expect(await visible('image#zm-color')).toBe(true);
  expect(await visible('image#zm-height')).toBe(false);

  await wrap.locator('.zonemap-mode[data-mode="height"]').click();
  expect(await visible('image#zm-height')).toBe(true);
  expect(await visible('image#zm-color')).toBe(false);
});

test('the chosen display mode persists across other zone maps', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${ZONE}`, { waitUntil: 'networkidle' });
  const wrap = page.locator('.zonetp-wrap');
  await wrap.waitFor({ state: 'visible', timeout: 10000 });
  await wrap.locator('.zonemap-mode[data-mode="color"]').click();

  // another zone: the remembered mode is applied on load
  await page.goto(`${baseURL}/zones/zone-ics/`, { waitUntil: 'networkidle' });
  const wrap2 = page.locator('.zonetp-wrap');
  await wrap2.waitFor({ state: 'visible', timeout: 10000 });
  await expect.poll(() => page.evaluate(() => {
    const c = document.querySelector('image#zm-color');
    const h = document.querySelector('image#zm-height');
    return !!c && !!h && c.style.display !== 'none' && h.style.display === 'none';
  })).toBe(true);
  const pressed = await wrap2.locator('.zonemap-mode[aria-pressed="true"]').getAttribute('data-mode');
  expect(pressed).toBe('color');
});

test('teleport elements are clickable and lead to the right zone page', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${ZONE}`, { waitUntil: 'networkidle' });
  await page.locator('.zonetp-wrap').waitFor({ state: 'visible', timeout: 10000 });
  const links = page.locator('.zonetp-wrap svg a');
  expect(await links.count()).toBeGreaterThanOrEqual(5);
  const hrefs = await links.evaluateAll((as) => as.map((a) => a.getAttribute('href')));
  for (const h of hrefs) expect(h).toMatch(/^\/zones\/[^/]+\/$/);

  // click one that leaves the current page, and land on the target zone
  const here = new URL(page.url()).pathname;
  const idx = Math.max(0, hrefs.findIndex((h) => h !== here));
  await links.nth(idx).locator('circle').click();
  await page.waitForLoadState('load');
  expect(new URL(page.url()).pathname).toBe(hrefs[idx]);
  expect(await page.locator('main h1').count()).toBe(1);
});

test('local teleport lines are visible and light up near their endpoint columns', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${ZONE}`, { waitUntil: 'networkidle' });
  const wrap = page.locator('.zonetp-wrap');
  await wrap.waitFor({ state: 'visible', timeout: 10000 });
  // this zone has in-zone teleports: the dashed lines are always visible
  const lines = page.locator('.zonetp-wrap .ltp-line');
  expect(await lines.count()).toBeGreaterThan(0);
  for (let i = 0; i < await lines.count(); i++) {
    expect(parseFloat(await lines.nth(i).evaluate((el) => getComputedStyle(el).opacity))).toBeGreaterThan(0.5);
  }
  // regression: every line carries real coordinates — the Python post-processor
  // once dropped digit-bearing attribute names (x1/y1/x2/y2), leaving
  // zero-length lines that rendered as nothing (2026-10-03, zone_TM)
  const degenerate = await lines.evaluateAll((ls) => ls.filter((l) => {
    const a = [l.getAttribute('x1'), l.getAttribute('y1'), l.getAttribute('x2'), l.getAttribute('y2')];
    return a.some((v) => v === null || v === '') ||
      (parseFloat(a[0]) === parseFloat(a[2]) && parseFloat(a[1]) === parseFloat(a[3]));
  }).length);
  expect(degenerate).toBe(0);
  // hovering near one of the endpoint columns brightens the line further
  const anchor = page.locator('.zonetp-wrap circle[data-ltp]').first();
  const b = await anchor.boundingBox();
  await page.mouse.move(b.x + b.width / 2, b.y + b.height / 2);
  await expect.poll(async () => {
    const on = await lines.evaluateAll((ls) => ls.some((l) => l.classList.contains('ltp-on')));
    return on;
  }, { timeout: 3000 }).toBe(true);
  const litId = await lines.evaluateAll((ls) =>
    ls.find((l) => l.classList.contains('ltp-on')).getAttribute('data-ltp'));
  // the lit line pairs with the hovered column
  expect(await anchor.getAttribute('data-ltp')).toContain(litId);
  // move away: it fades out again (hysteresis)
  const w = await wrap.boundingBox();
  await page.mouse.move(w.x + 8, w.y + 8);
  await expect.poll(async () => {
    return lines.evaluateAll((ls) => ls.every((l) => !l.classList.contains('ltp-on')));
  }, { timeout: 3000 }).toBe(true);
});

test('exit teleports point at the map edge toward their world-map direction', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${ZONE}`, { waitUntil: 'networkidle' });
  await page.locator('.zonetp-wrap').waitFor({ state: 'visible', timeout: 10000 });
  const info = await page.evaluate(() => {
    const svg = document.querySelector('.zonetp-wrap svg.zonemap');
    const vb = svg.viewBox.baseVal;
    const vw = vb.width, vh = vb.height;
    const fams = ['#41d3ff', '#6ee7a0', '#f5a05a', '#a78bfa', '#c8d2e0'];
    const lines = [...svg.querySelectorAll('line.exi-line')];
    return {
      count: lines.length,
      colorsOk: lines.every((l) => fams.includes(l.getAttribute('stroke'))),
      onBorder: lines.filter((l) => {
        const x = parseFloat(l.getAttribute('x2')), y = parseFloat(l.getAttribute('y2'));
        return Math.abs(x) < 0.01 || Math.abs(y) < 0.01 ||
               Math.abs(x - vw) < 0.01 || Math.abs(y - vh) < 0.01;
      }).length,
      casings: svg.querySelectorAll('.ltp-casing').length,
      ltps: svg.querySelectorAll('.ltp-line[data-ltp]').length,
    };
  });
  expect(info.count).toBeGreaterThan(0);
  expect(info.colorsOk).toBe(true);
  expect(info.onBorder).toBe(info.count); // every line reaches the map edge
  expect(info.casings).toBe(info.ltps); // one dark casing per local line
});

test('wheel over the zone map zooms it, not the page', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${ZONE}`, { waitUntil: 'networkidle' });
  const wrap = page.locator('.zonetp-wrap');
  await wrap.waitFor({ state: 'visible', timeout: 10000 });
  await wrap.scrollIntoViewIfNeeded();
  await page.waitForTimeout(100);
  const box = await wrap.boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  const y0 = await page.evaluate(() => window.scrollY);
  await page.mouse.wheel(0, -240);
  await page.waitForTimeout(50);
  const y1 = await page.evaluate(() => window.scrollY);
  const t = await wrap.locator('svg.zonemap').evaluate((el) => el.style.transform);
  expect(y1).toBe(y0); // page did not scroll
  expect(t).toMatch(/scale\((1\.[1-9]|[2-9])/);
});
