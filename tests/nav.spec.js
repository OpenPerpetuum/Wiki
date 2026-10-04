// Sidenav behaviour: groups (root -> sub) are open by default, 2nd-level
// sub-lists (Gamma's tiers, item-shop categories) start collapsed, and the
// carets collapse/expand any section.

const { test, expect } = require('@playwright/test');

// the sidenav only exists on wide viewports (drawer on mobile — see mobile.spec.js)
test.beforeEach(async ({ page }) => {
  test.skip(page.viewportSize().width <= 500, 'desktop layout only');
});

const VISIBLE_LINKS = () =>
  [...document.querySelectorAll('.sidenav a')].filter((a) => a.offsetParent !== null).length;
const COLLAPSED = (sel) =>
  [...document.querySelectorAll(sel)].map((g) => g.classList.contains('collapsed'));
const GROUPS = '.sidenav .nav-group'; // Start, World, Play, Systems, Reference
const SUBS = '.sidenav li li.nav-has-sub'; // 2nd level: Gamma, Missions, Production, Research, Character, Item shop, Content, Zones (8)

test('home: groups open, 2nd-level sub-lists closed', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`);
  expect(await page.locator('.nav-quick a').getAttribute('href')).toBe('/');
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([false, false, false, false, false]);
  expect(await page.evaluate(COLLAPSED, SUBS)).toEqual([true, true, true, true, true, true, true, true]);
  // Home + 5 group headers + Start(3) + World(6) + Play(12) + Systems(9) + Reference(6)
  // (the Missions/Recipes sub-entries are in collapsed sub-lists: not visible;
  //  the items catalog entry was in the collapsed Content sub: also not visible)
  expect(await page.evaluate(VISIBLE_LINKS)).toBe(42);
});

test('caret buttons collapse and re-open groups', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`);
  const carets = page.locator(`${GROUPS} > .nav-group-head .nav-caret`);
  await carets.nth(2).click(); // Play
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([false, false, true, false, false]);
  expect(await page.evaluate(VISIBLE_LINKS)).toBe(42 - 12);
  await carets.nth(2).click();
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([false, false, false, false, false]);
  expect(await page.evaluate(VISIBLE_LINKS)).toBe(42);
});

test('Play: Missions sub-list carries the mission data page', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/features/missions/`);
  const missSub = page.locator('.sidenav li.nav-has-sub:has(a[href="/features/missions/"]) > .nav-sub');
  expect(await missSub.locator('a:visible').count()).toBe(1);
  expect(await missSub.locator('a').first().getAttribute('href')).toBe('/content/missions/');
});

test('Systems: Production sub-list carries the recipes page', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/features/production/`);
  const prodSub = page.locator('.sidenav li.nav-has-sub:has(a[href="/features/production/"]) > .nav-sub');
  expect(await prodSub.locator('a:visible').count()).toBe(1);
  expect(await prodSub.locator('a').first().getAttribute('href')).toBe('/content/recipes/');
});

test('World entries visible by default, Gamma tiers expand on demand', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`);
  // Lore, Training, Protection levels, Alpha, Beta, Gamma (zone listings on
  // their own pages; the zone-type entries sit below Protection levels)
  expect(await page.locator(`${GROUPS} >> nth=1 >> .nav-items > li`).count()).toBe(6);
  const worldHrefs = await page.evaluate((sel) => {
    const g = document.querySelectorAll(sel)[1];
    return [...g.querySelectorAll('.nav-items > li > a, .nav-items > li .nav-sub-head > a')]
      .map((a) => a.getAttribute('href'));
  }, GROUPS);
  expect(worldHrefs).toEqual([
    '/features/lore/', '/zones/zone-training/', '/zones/protection/',
    '/zones/alpha/', '/zones/beta/', '/zones/gamma/',
  ]);
  const gammaCaret = page.locator('.sidenav .nav-has-sub:has(a[href="/zones/gamma/"]) > .nav-sub-head .nav-caret');
  expect(await page.locator('.map-sub-deep a:visible').count()).toBe(0);
  await gammaCaret.click();
  expect(await page.locator('.map-sub-deep a:visible').count()).toBe(5);
  await gammaCaret.click();
  expect(await page.locator('.map-sub-deep a:visible').count()).toBe(0);
});

test('feature page: all groups stay open, current page highlighted', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/features/combat/`);
  expect(await page.evaluate(COLLAPSED, GROUPS)).toEqual([false, false, false, false, false]);
  expect(await page.evaluate(COLLAPSED, SUBS)).toEqual([true, true, true, true, true, true, true, true]);
  expect(await page.locator('.sidenav a.active').textContent()).toBe('Combat');
});

test('items catalog index is gone', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`);
  expect(await page.locator('.sidenav a[href="/content/items/"]').count()).toBe(0);
  // the index page itself no longer builds
  const res = await page.request.get('/content/items/');
  expect(res.status()).toBe(404);
});

test('zones map: top-level World header is active', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/zones/map/`);
  expect(await page.locator('.sidenav a.active').textContent()).toBe('World');
});

test('gamma anchor hash: Gamma sub-list auto-opens, anchor highlighted', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/zones/gamma/#t2`);
  expect(await page.locator('.map-sub-deep a:visible').count()).toBe(5);
  // the T2 anchor itself is highlighted (the hash matches its full href)
  expect(await page.locator('.sidenav a.active').getAttribute('href')).toBe('/zones/gamma/#t2');
});

test('active selection box spans the whole row, ending after the caret', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/zones/map/`); // World header active
  const head = page.locator('.sidenav .nav-group-head.row-active');
  expect(await head.count()).toBe(1);
  const hb = await head.boundingBox();
  const cb = await head.locator('.nav-caret').boundingBox();
  // the box covers the caret: it starts before it and ends after it
  expect(hb.x).toBeLessThanOrEqual(cb.x + 1);
  expect(hb.x + hb.width).toBeGreaterThanOrEqual(cb.x + cb.width - 1);
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



test('content sub-page: Content tables auto-opens, section highlighted and in view', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/content/ores/crude/`);
  const contentSub = page.locator('.sidenav li.nav-has-sub:has(a[href="/content/"]) > .nav-sub');
  // missions and recipes moved out (to Play and Production) and the items
  // catalog index is gone: 5 entries
  expect(await contentSub.locator('a:visible').count()).toBe(5);
  const active = page.locator('.sidenav a.active');
  expect(await active.textContent()).toBe('Ores');
  const ar = await active.boundingBox();
  const nr = await page.locator('.sidenav').boundingBox();
  expect(ar.y >= nr.y - 1 && ar.y + ar.height <= nr.y + nr.height + 1).toBe(true);
});

test('zone data page: Zones sub-list auto-opens, family entry highlighted', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/zones/zone-asi/`);
  const zonesSub = page.locator('.sidenav li.nav-has-sub:has(a[href="/zones/"]) > .nav-sub');
  expect(await zonesSub.locator('a:visible').count()).toBe(2);
  // zone_ASI is protected: the Alpha family-listing entry is the active one
  expect(await page.locator('.sidenav a.active').getAttribute('href')).toBe('/zones/alpha/');
});

test('zone page: the family-listing entry (not Zones) is highlighted', async ({ page, baseURL }) => {
  // zone_ASI is alpha (protected): Alpha lights up, the plain /zones/ prefix does not
  await page.goto(`${baseURL}/zones/zone-asi/`);
  expect(await page.locator('.sidenav a.active').count()).toBe(1);
  expect(await page.locator('.sidenav a.active').getAttribute('href')).toBe('/zones/alpha/');
  // a generated beta zone page (open-PvP twin) highlights Beta
  await page.goto(`${baseURL}/zones/zone-asi-a-real/`);
  expect(await page.locator('.sidenav a.active').getAttribute('href')).toBe('/zones/beta/');
  // a gamma zone page highlights Gamma AND opens its tier sub-list
  await page.goto(`${baseURL}/zones/zone-gamma-z120/`);
  expect(await page.locator('.sidenav a.active').getAttribute('href')).toBe('/zones/gamma/');
  expect(await page.locator('.map-sub-deep a:visible').count()).toBe(5);
});

test('family-listing pages list their zones as cards', async ({ page, baseURL }) => {
  for (const [fam, min] of [['alpha', 6], ['beta', 10], ['gamma', 30]]) {
    await page.goto(`${baseURL}/zones/${fam}/`);
    const cards = page.locator('.zone-cards a.zone-card');
    expect(await cards.count(), fam).toBeGreaterThanOrEqual(min);
    // every card links to a zone page
    const href = await cards.first().getAttribute('href');
    expect(href).toMatch(/^\/zones\/zone-/);
  }
  // the gamma page carries the T0–T4 sections
  await page.goto(`${baseURL}/zones/gamma/`);
  for (const t of ['t0', 't1', 't2', 't3', 't4'])
    expect(await page.locator('#' + t).count(), t).toBe(1);
});

test('extensions page: Character sub-list auto-opens, Extensions highlighted', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/content/extensions/`);
  const charSub = page.locator('.sidenav li.nav-has-sub:has(a[href="/features/character/"]) > .nav-sub');
  expect(await charSub.locator('a:visible').count()).toBe(2);
  expect(await page.locator('.sidenav a.active').textContent()).toBe('Extensions');
});

test('tech tree page: Research sub-list auto-opens, Tech tree highlighted', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/content/techtree/`);
  const researchSub = page.locator('.sidenav li.nav-has-sub:has(a[href="/features/research/"]) > .nav-sub');
  expect(await researchSub.locator('a:visible').count()).toBe(1);
  expect(await page.locator('.sidenav a.active').textContent()).toBe('Tech tree');
});

// --- sidenav scroll position across navigation ---

const NAV_SCROLL = () => document.querySelector('.sidenav').scrollTop;

const ACTIVE_VISIBLE = () => {
  const nav = document.querySelector('.sidenav');
  const active = nav.querySelector('a.active');
  if (!active) return true; // e.g. the home page has no sidenav entry
  const nr = nav.getBoundingClientRect();
  const ar = active.getBoundingClientRect();
  return ar.top >= nr.top - 1 && ar.bottom <= nr.bottom + 1;
};

test('scroll position is preserved when the new active entry stays visible', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/features/robots/`);
  await page.waitForTimeout(400); // let the debounced scroll save (100 ms) flush
  const s1 = await page.evaluate(NAV_SCROLL);
  expect(s1).toBeGreaterThan(0); // robots is below the fold: the menu actually scrolled
  // Production sits right below Robots in the Systems group: visible at the
  // same scroll offset, so the position must carry over
  await page.locator('.sidenav a[href="/features/production/"]').click();
  await page.waitForLoadState('load');
  const s2 = await page.evaluate(NAV_SCROLL);
  expect(await page.locator('.sidenav a.active').textContent()).toBe('Production');
  expect(Math.abs(s2 - s1)).toBeLessThan(4);
});

test('every sidenav link lands with its active entry visible in the menu', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`);
  const hrefs = [...new Set(await page.$$eval('.sidenav a[href]', (as) => as.map((a) => a.getAttribute('href'))))];
  expect(hrefs.length).toBeGreaterThan(20);
  for (const href of hrefs) {
    await page.goto(`${baseURL}${href}`);
    const ok = await page.evaluate(ACTIVE_VISIBLE);
    expect(ok, `active entry not visible in the sidenav for ${href}`).toBe(true);
  }
});
