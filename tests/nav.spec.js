// Sidenav behaviour: groups (root -> sub) are open by default, 2nd-level
// sub-lists (Gamma's tiers, item-shop categories) start collapsed, and the
// carets collapse/expand any section.

const { test, expect } = require('@playwright/test');

const VISIBLE_LINKS = () =>
  [...document.querySelectorAll('.sidenav a')].filter((a) => a.offsetParent !== null).length;
const COLLAPSED = (sel) =>
  [...document.querySelectorAll(sel)].map((g) => g.classList.contains('collapsed'));
const GROUPS = '.sidenav .nav-group'; // Start, World, Play, Systems, Reference
const SUBS = '.sidenav li li.nav-has-sub'; // 2nd level: Gamma, Item shop

test('home: groups open, 2nd-level sub-lists closed', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`);
  expect(await page.locator('.nav-quick a').getAttribute('href')).toBe('/');
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([false, false, false, false, false]);
  expect(await page.evaluate(COLLAPSED, SUBS)).toEqual([true, true]);
  // Home + 5 group headers + Start(3) + World(5) + Play(12) + Systems(9) + Reference(9)
  expect(await page.evaluate(VISIBLE_LINKS)).toBe(44);
});

test('caret buttons collapse and re-open groups', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`);
  const carets = page.locator(`${GROUPS} > .nav-group-head .nav-caret`);
  await carets.nth(2).click(); // Play
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([false, false, true, false, false]);
  expect(await page.evaluate(VISIBLE_LINKS)).toBe(44 - 12);
  await carets.nth(2).click();
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([false, false, false, false, false]);
  expect(await page.evaluate(VISIBLE_LINKS)).toBe(44);
});

test('World anchors visible by default, Gamma tiers expand on demand', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`);
  // Training, Starter islands, Beta, Gamma, Protection levels
  expect(await page.locator(`${GROUPS} >> nth=1 >> .nav-items > li`).count()).toBe(5);
  const gammaCaret = page.locator('.sidenav .nav-has-sub:has(a[href="/zones/map/#t1"]) > .nav-sub-head .nav-caret');
  expect(await page.locator('.map-sub-deep a:visible').count()).toBe(0);
  await gammaCaret.click();
  expect(await page.locator('.map-sub-deep a:visible').count()).toBe(5);
  await gammaCaret.click();
  expect(await page.locator('.map-sub-deep a:visible').count()).toBe(0);
});

test('feature page: all groups stay open, current page highlighted', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/features/combat/`);
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([false, false, false, false, false]);
  expect(await page.evaluate(COLLAPSED, SUBS)).toEqual([true, true]);
  expect(await page.locator('.sidenav a.active').textContent()).toBe('Combat');
});

test('zones map: top-level World header is active', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/zones/map/`);
  expect(await page.locator('.sidenav a.active').textContent()).toBe('World');
});

test('item-shop sub-list expands on demand', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/features/market/`);
  const caret = page.locator(
    '.sidenav li.nav-has-sub:has(a[href="/content/shop/"]) > .nav-sub-head .nav-caret'
  );
  await caret.click();
  expect(await page.locator('.shop-sub a:visible').count()).toBe(11);
});

test('shop category page: sub-list auto-opens, active link scrolled into view', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/content/shop/ammo/`);
  expect(await page.locator('.shop-sub a:visible').count()).toBe(11);
  const active = page.locator('.sidenav a.active');
  expect(await active.textContent()).toBe('Ammo');
  // the sidenav must be scrolled so the active entry is inside its viewport
  const ar = await active.boundingBox();
  const nr = await page.locator('.sidenav').boundingBox();
  expect(ar.y >= nr.y - 1 && ar.y + ar.height <= nr.y + nr.height + 1).toBe(true);
});

test('gamma anchor hash: Gamma sub-list auto-opens', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/zones/map/#t2`);
  expect(await page.locator('.map-sub-deep a:visible').count()).toBe(5);
});
