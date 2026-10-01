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

test('dragging on the map pans it, reset restores the view', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${MAP}`);
  const wrap = page.locator('.zonemap-wrap');
  await wrap.scrollIntoViewIfNeeded();
  await page.waitForTimeout(100);
  const box = await wrap.boundingBox();
  const cx = box.x + box.width / 2, cy = box.y + box.height / 2;

  await page.mouse.move(cx, cy);
  await page.mouse.down();
  await page.mouse.move(cx + 60, cy + 40, { steps: 5 });
  await page.mouse.up();
  const p = await transform(page);
  expect(p.tx).toBeGreaterThan(20);
  expect(p.ty).toBeGreaterThan(20);

  await page.locator('.zonemap-reset').click();
  const r = await transform(page);
  expect(r.scale).toBe(1);
  expect(r.tx).toBe(0);
  expect(r.ty).toBe(0);
});
