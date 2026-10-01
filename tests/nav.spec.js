// Sidenav behaviour: the high-level default view, the group/sub-list caret
// buttons, and the current-page auto-expand + active-link highlight.

const { test, expect } = require('@playwright/test');

const VISIBLE_LINKS = () =>
  [...document.querySelectorAll('.sidenav a')].filter((a) => a.offsetParent !== null).length;
const COLLAPSED = (sel) =>
  [...document.querySelectorAll(sel)].map((g) => g.classList.contains('collapsed'));
const GROUPS = '.sidenav .nav-group';
const SUBS = '.sidenav .nav-has-sub';

test('home shows the high-level view (top links + 4 collapsed groups)', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`);
  expect(await page.locator('.nav-quick a').getAttribute('href')).toBe('/');
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([true, true, true, true]);
  expect(await page.evaluate(COLLAPSED, SUBS)).toEqual([true, true, true]);
  // Home, World, All features + the four group headers
  expect(await page.evaluate(VISIBLE_LINKS)).toBe(7);
});

test('World is top-level and its sub-list expands on demand', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`);
  const caret = page.locator('.sidenav li.nav-top.nav-has-sub > .nav-sub-head .nav-caret');
  await caret.click();
  expect(await page.locator('.map-sub a:visible').count()).toBeGreaterThanOrEqual(5);
  await caret.click();
  expect(await page.locator('.map-sub a:visible').count()).toBe(0);
});

test('zones map: top-level World link is active', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/zones/map/`);
  // Reference covers /zones/ (its "Zones" entry) and auto-expands; the
  // top-level World link is the longest match, so it gets the highlight.
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([true, true, true, false]);
  expect(await page.locator('.sidenav a.active').textContent()).toBe('World');
});

test('caret buttons expand and collapse groups', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`);
  const carets = page.locator(`${GROUPS} > .nav-group-head .nav-caret`);
  await carets.nth(1).click(); // Play
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([true, false, true, true]);
  expect(await page.evaluate(VISIBLE_LINKS)).toBeGreaterThan(7);
  await carets.nth(1).click();
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([true, true, true, true]);
});

test('current-page group auto-expands, sub-lists stay collapsed', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/features/combat/`);
  // groups: Start, Play, Systems, Reference — Play contains /features/combat/
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([true, false, true, true]);
  expect(await page.evaluate(COLLAPSED, SUBS)).toEqual([true, true, true]);
  expect(await page.locator('.sidenav a.active').textContent()).toBe('Combat');
});

test('item-shop sub-list expands on demand', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/features/market/`);
  const caret = page.locator(
    '.sidenav li.nav-has-sub:has(a[href="/content/shop/"]) > .nav-sub-head .nav-caret'
  );
  await caret.click();
  expect(await page.locator('.shop-sub a:visible').count()).toBe(11);
});
