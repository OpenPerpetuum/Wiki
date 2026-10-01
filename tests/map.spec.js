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

test('at max zoom out the whole map is visible and cannot be panned away', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${MAP}`);
  const wrap = page.locator('.zonemap-wrap');
  await wrap.scrollIntoViewIfNeeded();
  await page.waitForTimeout(100);
  const box = await wrap.boundingBox();
  const cx = box.x + box.width / 2, cy = box.y + box.height / 2;

  // no zoom: the map fits, so dragging must not move it
  await page.mouse.move(cx, cy);
  await page.mouse.down();
  await page.mouse.move(cx + 100, cy + 100, { steps: 3 });
  await page.mouse.up();
  const p = await transform(page);
  expect(p.tx).toBe(0);
  expect(p.ty).toBe(0);
  expect(p.scale).toBe(1);
});

test('zoomed in: drag pans but the map always covers the view; reset restores', async ({ page, baseURL }) => {
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

  const covered = () => page.evaluate(() => {
    const w = document.querySelector('.zonemap-wrap').getBoundingClientRect();
    const m = document.querySelector('.zonemap').getBoundingClientRect();
    return m.top <= w.top + 1 && m.left <= w.left + 1
        && m.bottom >= w.bottom - 1 && m.right >= w.right - 1;
  });

  // drag far past any sane limit — the map must stay in view
  await page.mouse.move(cx, cy);
  await page.mouse.down();
  await page.mouse.move(cx + 600, cy + 600, { steps: 5 });
  await page.mouse.up();
  let p = await transform(page);
  expect(p.ty).toBeGreaterThan(20); // it did pan
  expect(await covered()).toBe(true);

  // and the other way
  await page.mouse.move(cx, cy);
  await page.mouse.down();
  await page.mouse.move(cx - 1200, cy - 1200, { steps: 5 });
  await page.mouse.up();
  p = await transform(page);
  expect(p.ty).toBeLessThan(-20);
  expect(await covered()).toBe(true);

  await page.locator('.zonemap-reset').click();
  const r = await transform(page);
  expect(r.scale).toBe(1);
  expect(r.tx).toBe(0);
  expect(r.ty).toBe(0);
});
