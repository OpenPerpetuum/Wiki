// World map interactions: wheel over the map zooms (the page must NOT
// scroll), wheel elsewhere still scrolls, drag pans, reset restores.
// map.js used to run in <head> before the DOM existed, crash on
// document.body === null, and never attach any of these handlers.
const { test, expect } = require('@playwright/test');

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
  const section = page.locator('h2#training');
  await section.scrollIntoViewIfNeeded();
  await page.waitForTimeout(100);
  const box = await section.boundingBox();
  await page.mouse.move(box.x + 10, box.y + 10); // over text, not the map
  const y0 = await page.evaluate(() => window.scrollY);
  await page.mouse.wheel(0, 300);
  await page.waitForTimeout(50);
  const y1 = await page.evaluate(() => window.scrollY);
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
