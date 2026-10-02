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

test('display modes: color and plain switch the background', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${ZONE}`, { waitUntil: 'networkidle' });
  const wrap = page.locator('.zonetp-wrap');
  await wrap.waitFor({ state: 'visible', timeout: 10000 });
  const visible = (sel) => page.evaluate((s) => {
    const el = document.querySelector(s);
    return el && el.style.display !== 'none';
  }, sel);
  expect(await visible('image#zm-height')).toBe(true);
  expect(await visible('image#zm-color')).toBe(false);

  await wrap.locator('.zonemap-mode[data-mode="color"]').click();
  expect(await visible('image#zm-color')).toBe(true);
  expect(await visible('image#zm-height')).toBe(false);

  await wrap.locator('.zonemap-mode[data-mode="plain"]').click();
  expect(await visible('image#zm-height')).toBe(false);
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
