// Zone teleport maps: inlined at runtime from /zonemaps/*.svg (zone-map.js)
// and made interactive by map.js — wheel zoom, pan, reset — with their
// teleport elements clickable, linking to the zone pages.
const { test, expect } = require('@playwright/test');

// Hokkogaros — 10 labelled teleport columns
const ZONE = '/zones/zone-asi-a-real/';

test('zone map is inlined with its heightmap background', async ({ page, baseURL }) => {
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto(`${baseURL}${ZONE}`, { waitUntil: 'networkidle' });
  const wrap = page.locator('.zonetp-wrap');
  await wrap.waitFor({ state: 'visible', timeout: 10000 });
  const svg = wrap.locator('svg.zonemap');
  expect(await svg.count()).toBe(1);
  expect(await page.locator('img[src^="/zonemaps/"]').count()).toBe(0); // img replaced
  const bg = svg.locator('image');
  expect(await bg.count()).toBe(1);
  expect(await bg.getAttribute('href')).toContain('data:image/png;base64,');
  expect(errors).toEqual([]);
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
