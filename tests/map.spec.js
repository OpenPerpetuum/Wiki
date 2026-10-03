// World map interactions: wheel over the map zooms (the page must NOT
// scroll), wheel elsewhere still scrolls, drag pans, reset restores.
// map.js used to run in <head> before the DOM existed, crash on
// document.body === null, and never attach any of these handlers.
const { test, expect } = require('@playwright/test');

// desktop-only: these drive the mouse over fixed desktop map geometry
test.beforeEach(async ({ page }) => {
  test.skip(page.viewportSize().width <= 500, 'desktop layout only');
});

const MAP = '/zones/map/';

const transform = (page) =>
  page.locator('.zonemap').evaluate((el) => {
    const t = el.style.transform || '';
    const tr = /translate\(([-\d.]+)px,\s*([-\d.]+)px\)/.exec(t);
    const sc = /scale\(([-\d.]+)\)/.exec(t);
    return {
      tx: tr ? parseFloat(tr[1]) : 0,
      ty: tr ? parseFloat(tr[2]) : 0,
      scale: sc ? parseFloat(sc[1]) : 1
    };
  });

test('map page loads without script errors', async ({ page, baseURL }) => {
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto(`${baseURL}/`);
  await page.goto(`${baseURL}${MAP}`);
  expect(errors).toEqual([]);
});

test('zones are drawn with coastline thumbnails and inter-zone TPs are dashed', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${MAP}`, { waitUntil: 'networkidle' });
  // every zone node carries its coastline thumbnail (thumb.png) — the same
  // design as the index cards below the map
  const imgs = page.locator('.zonemap image[href$="thumb.png"]');
  expect(await imgs.count()).toBeGreaterThanOrEqual(80);
  const first = await imgs.first().getAttribute('href');
  const r = await page.request.get(first);
  expect(r.ok()).toBe(true);
  expect(r.headers()['content-type']).toMatch(/image\/png/);
  // inter-zone TP lines: dashed, gradient-colored (stroke = url(#tpgN)),
  // no local-teleport elements on the world map anymore
  const tps = page.locator('.zonemap .zonemap-tp');
  expect(await tps.count()).toBeGreaterThan(10);
  const dash = await tps.first().evaluate((el) => getComputedStyle(el).strokeDasharray);
  expect(dash).not.toBe('none');
  expect(await tps.first().getAttribute('stroke')).toMatch(/^url\(#tpg\d+\)$/);
  expect(await page.locator('.zonemap .ltp-line').count()).toBe(0);
  expect(await page.locator('.zonemap .ltp-dot').count()).toBe(0);
});

test('the map page links to the family-listing pages (no card sections of its own)', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${MAP}`, { waitUntil: 'networkidle' });
  // the per-family card grids moved to /zones/alpha|beta|gamma/ — the map
  // page only carries the map + legend + links
  expect(await page.locator('.zone-card').count()).toBe(0);
  for (const fam of ['alpha', 'beta', 'gamma'])
    expect(await page.locator('a[href=\'/zones/' + fam + '/\']').count(), fam).toBeGreaterThan(0);
});

test('TP lines draw OVER the islands and anchor at the real column/landing spots', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${MAP}`, { waitUntil: 'networkidle' });
  const info = await page.evaluate(() => {
    const svg = document.querySelector('.zonemap');
    const nodes = [...svg.querySelectorAll('a > g')];
    const lines = [...svg.querySelectorAll('line.zonemap-tp')];
    // 1) the lines come after every node in document order (drawn on top)
    const lastNode = nodes[nodes.length - 1];
    const firstLine = lines[0];
    const onTop = lastNode.compareDocumentPosition(firstLine) & Node.DOCUMENT_POSITION_FOLLOWING;
    // 2) at least one line's endpoint is offset from its zone node's center
    //    (the column/landing's real position inside the zone), not all of
    //    them run center-to-center
    const center = (n) => {
      const s = n.querySelector('rect, circle');
      return s && s.tagName === 'rect'
        ? { x: parseFloat(s.getAttribute('x')) + s.getAttribute('width') / 2, y: parseFloat(s.getAttribute('y')) + s.getAttribute('height') / 2 }
        : { x: parseFloat(s.getAttribute('cx')), y: parseFloat(s.getAttribute('cy')) };
    };
    const byHref = {};
    nodes.forEach((n) => { byHref[n.closest('a').getAttribute('href')] = center(n); });
    let anchored = 0, total = 0;
    lines.forEach((l) => {
      const t = l.querySelector('title').textContent;
      const [src] = t.split(' — ');
      const c = byHref['/zones/' + src.toLowerCase().replace(/_/g, '-') + '/'];
      if (!c) return;
      total++;
      const dx = Math.abs(parseFloat(l.getAttribute('x1')) - c.x);
      const dy = Math.abs(parseFloat(l.getAttribute('y1')) - c.y);
      if (dx > 0.5 || dy > 0.5) anchored++;
    });
    return { onTop: !!onTop, anchored, total, lines: lines.length };
  });
  expect(info.onTop).toBe(true);
  expect(info.total).toBeGreaterThan(0);
  expect(info.anchored).toBeGreaterThan(0);
});

test('TP lines are nearly invisible at rest and fade in near the cursor (desktop)', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${MAP}`, { waitUntil: 'networkidle' });
  // point on the first TP line, in screen coordinates (CTM handles zoom/pan)
  const pt = await page.evaluate(() => {
    const line = document.querySelector('.zonemap-tp');
    const svg = document.querySelector('.zonemap');
    const p = svg.createSVGPoint();
    p.x = (+line.getAttribute('x1') + +line.getAttribute('x2')) / 2;
    p.y = (+line.getAttribute('y1') + +line.getAttribute('y2')) / 2;
    const s = p.matrixTransform(svg.getScreenCTM());
    return { x: s.x, y: s.y };
  });
  const op = () => page.evaluate(() => getComputedStyle(document.querySelector('.zonemap-tp')).strokeOpacity);
  // at rest: almost invisible
  expect(parseFloat(await op())).toBeLessThan(0.15);
  // cursor on the line: it lights up
  await page.mouse.move(pt.x, pt.y);
  await page.waitForTimeout(300);
  expect(parseFloat(await op())).toBeGreaterThan(0.8);
  // cursor far away: back to nearly invisible
  await page.mouse.move(5, 5);
  await page.waitForTimeout(400);
  expect(parseFloat(await op())).toBeLessThan(0.15);
});

test('wheel over the map zooms the map and does not scroll the page', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${MAP}`);
  const wrap = page.locator('.zonemap-wrap');
  await wrap.scrollIntoViewIfNeeded();
  await page.waitForTimeout(100); // let the scroll settle
  const box = await wrap.boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);

  const y0 = await page.evaluate(() => window.scrollY);
  await page.mouse.wheel(0, -240); // zoom in
  await page.waitForTimeout(50);
  const y1 = await page.evaluate(() => window.scrollY);
  const in_ = await transform(page);
  expect(y1).toBe(y0); // the page did not scroll
  expect(in_.scale).toBeGreaterThan(1.1);

  await page.mouse.wheel(0, 240); // zoom out
  await page.waitForTimeout(50);
  const out = await transform(page);
  expect(out.scale).toBeLessThan(in_.scale);
});

test('wheel outside the map still scrolls the page', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${MAP}`);
  const section = page.locator('.zonemap-legend');
  await section.scrollIntoViewIfNeeded();
  await page.waitForTimeout(100);
  const box = await section.boundingBox();
  await page.mouse.move(box.x + 10, box.y + 10); // over text, not the map
  const y0 = await page.evaluate(() => window.scrollY);
  await page.mouse.wheel(0, 300);
  await page.waitForTimeout(50);
  let y1 = await page.evaluate(() => window.scrollY);
  // the map page is short: if the wheel lands at the bottom of the document
  // there is nothing left to scroll — scroll back to the top and retry
  if (y1 === y0) {
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.waitForTimeout(100);
    await page.mouse.move(box.x + 10, Math.max(10, box.y + 10));
    await page.mouse.wheel(0, 300);
    await page.waitForTimeout(50);
    y1 = await page.evaluate(() => window.scrollY);
    expect(y1).toBeGreaterThan(0);
    return;
  }
  expect(y1).toBeGreaterThan(y0);
});

// Share of the viewport still covered by map content (0..1). Recomputed
// from the same geometry the clamp uses: letterboxed content, centered,
// with the current translate/scale applied.
const coverage = (page) =>
  page.evaluate(() => {
    const svg = document.querySelector('.zonemap');
    const wrap = document.querySelector('.zonemap-wrap');
    const st = getComputedStyle(svg);
    const vw = svg.clientWidth - parseFloat(st.paddingLeft) - parseFloat(st.paddingRight);
    const vh = svg.clientHeight - parseFloat(st.paddingTop) - parseFloat(st.paddingBottom);
    const vb = svg.viewBox.baseVal;
    const k = Math.min(vw / vb.width, vh / vb.height);
    const m = /translate\(([-\d.]+)px,\s*([\d.-]+)px\) scale\(([-\d.]+)\)/.exec(svg.style.transform || '');
    const tx = m ? +m[1] : 0, ty = m ? +m[2] : 0, s = m ? +m[3] : 1;
    const W = wrap.clientWidth, H = wrap.clientHeight;
    const cw = s * vb.width * k, ch = s * vb.height * k;
    const ox = Math.max(0, Math.min((W - cw) / 2 + tx + cw, W) - Math.max((W - cw) / 2 + tx, 0));
    const oy = Math.max(0, Math.min((H - ch) / 2 + ty + ch, H) - Math.max((H - ch) / 2 + ty, 0));
    return (ox * oy) / (W * H);
  });

test('panning may show empty space, but at least 25% of the viewport stays covered', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${MAP}`);
  const wrap = page.locator('.zonemap-wrap');
  await wrap.scrollIntoViewIfNeeded();
  await page.waitForTimeout(100);
  const box = await wrap.boundingBox();
  const cx = box.x + box.width / 2, cy = box.y + box.height / 2;

  // at max zoom out the whole map is visible: nothing is clipped, so the
  // coverage equals the map's own share of the (wider) viewport
  expect(await coverage(page)).toBeGreaterThanOrEqual(0.36);

  // ...and it can be panned (empty space allowed)...
  await page.mouse.move(cx, cy);
  await page.mouse.down();
  await page.mouse.move(cx + 100, cy + 60, { steps: 3 });
  await page.mouse.up();
  let p = await transform(page);
  expect(p.tx).toBeGreaterThan(50);

  // ...but never further out than 25% of the viewport still showing the map
  // (many small steps: like a real drag, the pointer leaves the wrapper
  // only after pointer capture has engaged)
  await page.mouse.move(cx, cy);
  await page.mouse.down();
  await page.mouse.move(cx + 3000, cy + 3000, { steps: 40 });
  await page.mouse.up();
  expect(await coverage(page)).toBeGreaterThanOrEqual(0.249);
  p = await transform(page);
  expect(p.tx + p.ty).toBeGreaterThan(100); // it did pan as far as the limit allows

  await page.locator('.zonemap-reset').click();
  const r = await transform(page);
  expect(r.scale).toBe(1);
  expect(r.tx).toBe(0);
  expect(r.ty).toBe(0);
});

test('zoomed in: panning keeps at least 25% of the viewport covered', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${MAP}`);
  const wrap = page.locator('.zonemap-wrap');
  await wrap.scrollIntoViewIfNeeded();
  await page.waitForTimeout(100);
  const box = await wrap.boundingBox();
  const cx = box.x + box.width / 2, cy = box.y + box.height / 2;

  // zoom in (~2x)
  await page.mouse.move(cx, cy);
  await page.mouse.wheel(0, -480);
  await page.waitForTimeout(50);
  expect((await transform(page)).scale).toBeGreaterThan(1.5);

  // drag far past any sane limit — at least 25% must stay covered
  await page.mouse.move(cx, cy);
  await page.mouse.down();
  await page.mouse.move(cx + 600, cy + 600, { steps: 20 });
  await page.mouse.up();
  let p = await transform(page);
  expect(p.ty).toBeGreaterThan(20); // it did pan
  expect(await coverage(page)).toBeGreaterThanOrEqual(0.249);

  // and the other way
  await page.mouse.move(cx, cy);
  await page.mouse.down();
  await page.mouse.move(cx - 1200, cy - 1200, { steps: 20 });
  await page.mouse.up();
  p = await transform(page);
  expect(p.ty).toBeLessThan(-20);
  expect(await coverage(page)).toBeGreaterThanOrEqual(0.249);
});
