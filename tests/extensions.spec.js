// Extensions page: the categories overview is an inline (not <img>) SVG whose
// boxes are links to the per-category sections below, and the extension
// cards emphasize rank/credits/bonus.
const { test, expect } = require('@playwright/test');

const EXT = '/content/extensions/';

test('category overview boxes link to the category sections', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${EXT}`, { waitUntil: 'networkidle' });
  // the overview is embedded inline so its <a> boxes are real links
  const wrap = page.locator('.map-zoom-wrap.extcats-wrap');
  await wrap.waitFor({ state: 'visible', timeout: 10000 });
  expect(await wrap.locator('svg').count()).toBe(1);
  const links = wrap.locator('svg a[href^="#cat-"]');
  expect(await links.count()).toBeGreaterThanOrEqual(10);
  // every target anchor exists on the page
  const hrefs = await links.evaluateAll((as) => as.map((a) => a.getAttribute('href')));
  for (const h of hrefs) {
    expect(await page.locator(`a[id="${h.slice(1)}"]`).count()).toBe(1);
  }
  // clicking a box jumps to its section (no navigation, the anchor scrolls)
  const before = page.url();
  await links.first().click();
  await page.waitForTimeout(150);
  expect(page.url()).not.toBe(before);
  expect(new URL(page.url()).hash).toMatch(/^#cat-/);
  expect(new URL(before).pathname).toBe(new URL(page.url()).pathname);
});

test('extension cards emphasize rank, credits and bonus', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${EXT}`, { waitUntil: 'networkidle' });
  const cards = page.locator('.ext-card');
  expect(await cards.count()).toBeGreaterThanOrEqual(100);
  const first = cards.first();
  for (const cls of ['ext-val-rank', 'ext-val-price', 'ext-val-bonus']) {
    const el = first.locator(`.${cls}`);
    expect(await el.count()).toBe(1);
    const style = await el.evaluate((e) => {
      const s = getComputedStyle(e);
      return { weight: s.fontWeight, color: s.color };
    });
    expect(Number(style.weight)).toBeGreaterThanOrEqual(700);
  }
  // the tree section is gone (the overview boxes replaced it)
  expect(await page.locator('a[id="tree"]').count()).toBe(0);
});
