// Mobile layout (390x844 project, see playwright.config.js): the page must
// never develop a horizontal scroll. Wide content (large tables, mermaid
// diagrams, video embeds) may overflow — but only inside a container that
// scrolls or clips it (the "large table" exception: same information,
// different display format).
const { test, expect } = require('@playwright/test');

// representative spread: prose, mermaid diagrams, wide tables, cards, maps,
// the click-to-play video grid
const PAGES = [
  '/',                              // video grid + click-to-play embed
  '/features/combat/',              // mermaid + tables
  '/features/pbs/',
  '/features/probes/',
  '/features/sparks/',
  '/features/transport/',
  '/features/market/',
  '/features/items/',
  '/features/robots/',
  '/features/abbreviations/',       // wide table
  '/content/missions/',             // mission cards + reward links
  '/content/stat-reference/',       // large tables
  '/content/ores/',
  '/content/robots/',
  '/content/recipes/',
  '/zones/',                        // zone index table
  '/zones/map/',                    // world map
  '/zones/zone-asi-a-real/',        // zone teleport map
];

// Elements whose right edge is past the viewport are allowed only if they
// sit inside a container that scrolls/clips the overflow.
function overflowReport() {
  const vw = document.documentElement.clientWidth;
  const bad = [];
  for (const el of document.querySelectorAll('body *')) {
    const r = el.getBoundingClientRect();
    if ((r.width === 0 && r.height === 0) || r.right <= vw + 1) continue;
    let clipped = false;
    for (let n = el.parentNode; n && n !== document.body; n = n.parentNode) {
      const s = getComputedStyle(n);
      if (['auto', 'scroll', 'hidden', 'clip'].includes(s.overflowX) &&
          n.scrollWidth >= n.clientWidth) { clipped = true; break; }
    }
    if (!clipped)
      bad.push(el.tagName + '.' + String(el.className).trim().split(/\s+/).slice(0, 2).join('.') +
               ' right=' + Math.round(r.right) + ' (viewport ' + vw + ')');
    if (bad.length > 8) break;
  }
  return bad;
}

test.beforeEach(async ({ page }) => {
  test.skip(page.viewportSize().width > 500, 'mobile layout only');
});

for (const path of PAGES) {
  test(`no horizontal scroll on ${path}`, async ({ page, baseURL }) => {
    await page.goto(`${baseURL}${path}`, { waitUntil: 'networkidle' });
    await expect.poll(
      () => page.evaluate(() =>
        document.documentElement.scrollWidth - document.documentElement.clientWidth),
      { timeout: 15000 }).toBeLessThanOrEqual(1);
    const bad = await page.evaluate(overflowReport);
    expect(bad, 'elements past the viewport edge: ' + bad.join('; ')).toEqual([]);
  });
}

test('large tables scroll inside themselves, not the page', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/content/stat-reference/`, { waitUntil: 'networkidle' });
  const t = await page.evaluate(() => {
    const tables = [...document.querySelectorAll('main table')];
    const wide = tables.find((tb) => tb.scrollWidth > tb.clientWidth + 4);
    if (!wide) return null;
    const r = wide.getBoundingClientRect();
    return { scrolls: true, fitsViewport: r.right <= document.documentElement.clientWidth + 1,
             sw: wide.scrollWidth, cw: wide.clientWidth };
  });
  expect(t, 'expected a wide table that scrolls inside itself').not.toBeNull();
  expect(t.fitsViewport).toBe(true);
});

test('mermaid diagrams fit the column or scroll inside their box', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/features/combat/`, { waitUntil: 'networkidle' });
  await page.waitForSelector('.mermaid svg', { timeout: 15000 });
  const bad = await page.evaluate(() => {
    const out = [];
    for (const box of document.querySelectorAll('.mermaid')) {
      const br = box.getBoundingClientRect();
      if (br.right > document.documentElement.clientWidth + 1)
        out.push('diagram box past viewport: ' + Math.round(br.right));
    }
    return out;
  });
  expect(bad).toEqual([]);
});

test('topbar stays fixed at the top while scrolling', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/features/robots/`, { waitUntil: 'networkidle' });
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight / 2));
  await page.waitForTimeout(100);
  const top = await page.locator('.topbar').boundingBox();
  expect(top.y).toBe(0);
});

test('sidenav drawer: hamburger opens, backdrop/link/swipe/Escape close', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/features/items/`, { waitUntil: 'networkidle' });
  const toggle = page.locator('#sidenav-toggle');
  const drawer = page.locator('.sidenav');
  const openState = () => page.evaluate(() => document.body.classList.contains('sidenav-open'));

  expect(await toggle.isVisible()).toBe(true); // hamburger only on mobile
  expect(await drawer.evaluate((el) => el.getBoundingClientRect().right)).toBeLessThan(0); // closed: off-screen

  // tap the hamburger -> drawer slides in
  await toggle.click();
  await expect.poll(() => drawer.evaluate((el) => el.getBoundingClientRect().right), { timeout: 5000 })
    .toBeGreaterThan(100);
  expect(await openState()).toBe(true);
  expect(await toggle.getAttribute('aria-expanded')).toBe('true');

  // tap the dimmed backdrop (content area) -> closes
  await page.locator('main').click({ position: { x: 300, y: 200 } });
  await expect.poll(openState).toBe(false);

  // open again, tap a link -> navigates AND closes
  await toggle.click();
  await expect.poll(openState).toBe(true);
  await drawer.locator('a[href$="/features/combat/"]').click();
  await page.waitForURL('**/features/combat/');
  await expect.poll(openState).toBe(false);

  // open again, swipe the drawer left (touch) -> closes
  await toggle.click();
  await expect.poll(openState).toBe(true);
  await drawer.evaluate((el) => {
    const touch = (x) => new Touch({ identifier: 1, target: el, clientX: x, clientY: 300 });
    el.dispatchEvent(new TouchEvent('touchstart', { touches: [touch(200)], bubbles: true }));
    el.dispatchEvent(new TouchEvent('touchmove', { touches: [touch(80)], bubbles: true }));
  });
  await expect.poll(openState).toBe(false);

  // open again, Escape -> closes
  await toggle.click();
  await expect.poll(openState).toBe(true);
  await page.keyboard.press('Escape');
  await expect.poll(openState).toBe(false);
});

test('video embed fits its card on click-to-play', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/`, { waitUntil: 'networkidle' });
  const card = page.locator('.video-card').first();
  await card.click();
  const iframe = card.locator('iframe');
  await iframe.waitFor({ state: 'attached', timeout: 10000 });
  await page.waitForTimeout(150);
  const fit = await page.evaluate(() => {
    const card = document.querySelector('.video-card');
    const f = card.querySelector('iframe');
    const cr = card.getBoundingClientRect(), fr = f.getBoundingClientRect();
    return {
      cardInViewport: cr.right <= document.documentElement.clientWidth + 1,
      iframeFits: fr.width <= cr.width + 1 && fr.height <= cr.height + 1,
      ratio: cr.height / cr.width, // ~16:9
    };
  });
  expect(fit.cardInViewport).toBe(true);
  expect(fit.iframeFits).toBe(true);
  expect(fit.ratio).toBeGreaterThan(0.4); // still a video shape, not collapsed
});
