// Sidenav behaviour: groups (root -> sub) are open by default, 2nd-level
// sub-lists (Gamma's tiers, item-shop categories) start collapsed, and the
// carets collapse/expand any section.

const { test, expect } = require('@playwright/test');

const VISIBLE_LINKS = () =>
  [...document.querySelectorAll('.sidenav a')].filter((a) => a.offsetParent !== null).length;
const COLLAPSED = (sel) =>
  [...document.querySelectorAll(sel)].map((g) => g.classList.contains('collapsed'));
const GROUPS = '.sidenav .nav-group'; // Start, World, Play, Systems, Reference
const SUBS = '.sidenav li li.nav-has-sub'; // 2nd level: Gamma, Item shop, Content, Zones

test('home: groups open, 2nd-level sub-lists closed', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`);
  expect(await page.locator('.nav-quick a').getAttribute('href')).toBe('/');
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([false, false, false, false, false]);
  expect(await page.evaluate(COLLAPSED, SUBS)).toEqual([true, true, true, true]);
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
  expect(await page.evaluate(COLLAPSED, SUBS)).toEqual([true, true, true, true]);
  expect(await page.locator('.sidenav a.active').textContent()).toBe('Combat');
});

test('zones map: top-level World header is active', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/zones/map/`);
  expect(await page.locator('.sidenav a.active').textContent()).toBe('World');
});

test('world anchor click: the anchor itself is highlighted, not World', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/zones/map/`);
  await page.locator('.sidenav a[href="/zones/map/#training"]').click();
  // the highlight follows the hashchange event, which fires asynchronously
  await page.waitForFunction(
    () => {
      const actives = document.querySelectorAll('.sidenav a.active');
      return actives.length === 1 && actives[0].getAttribute('href') === '/zones/map/#training';
    },
    null,
    { timeout: 5000 }
  );
  const actives = page.locator('.sidenav a.active');
  expect(await actives.count()).toBe(1);
  expect(await actives.getAttribute('href')).toBe('/zones/map/#training');
});

test('active selection spans the row up to the caret button', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/zones/map/`); // World header active
  const head = page.locator('.sidenav .nav-group-head:has(.active)');
  const lb = await head.locator('a').boundingBox();
  const cb = await head.locator('.nav-caret').boundingBox();
  // only the flex gap (0.5rem) may remain between the selection and the caret
  expect(cb.x - (lb.x + lb.width)).toBeLessThan(12);
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

test('content sub-page: Content tables auto-opens, section highlighted and in view', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/content/ores/crude/`);
  const contentSub = page.locator('.sidenav li.nav-has-sub:has(a[href="/content/"]) > .nav-sub');
  expect(await contentSub.locator('a:visible').count()).toBe(9);
  const active = page.locator('.sidenav a.active');
  expect(await active.textContent()).toBe('Ores');
  const ar = await active.boundingBox();
  const nr = await page.locator('.sidenav').boundingBox();
  expect(ar.y >= nr.y - 1 && ar.y + ar.height <= nr.y + nr.height + 1).toBe(true);
});

test('zone data page: Zones sub-list auto-opens', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/zones/zone-asi/`);
  const zonesSub = page.locator('.sidenav li.nav-has-sub:has(a[href="/zones/"]) > .nav-sub');
  expect(await zonesSub.locator('a:visible').count()).toBe(2);
  expect(await page.locator('.sidenav a.active').textContent()).toBe('Zones');
});
