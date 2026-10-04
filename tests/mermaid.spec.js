// Parses every mermaid diagram in the wiki sources with the site's own
// mermaid build (public/mermaid.min.js), so a syntax regression in any of the
// ~200 generated/handwritten diagrams fails the suite instead of rendering a
// broken diagram on the live page.

const { test, expect } = require('@playwright/test');
const path = require('path');
const { collectBlocks } = require('./mermaid-blocks');

test('every mermaid diagram in content/ parses', async ({ page, baseURL }) => {
  const blocks = collectBlocks(path.join(__dirname, '..', 'content'));
  // Guard against a broken extractor (the suite must not pass on zero blocks).
  expect(blocks.length).toBeGreaterThan(100);

  // Load a real page: it includes the site's mermaid.min.js (defer).
  await page.goto(`${baseURL}/features/pbs/`);
  await page.waitForFunction(() => window.mermaid && typeof window.mermaid.parse === 'function');

  const failures = await page.evaluate(async (blocks) => {
    mermaid.initialize({ startOnLoad: false, theme: 'dark' });
    const out = [];
    for (const b of blocks) {
      try {
        await mermaid.parse(b.src);
      } catch (e) {
        out.push({ file: b.file, block: b.n, err: String((e && e.message) || e).slice(0, 200) });
      }
    }
    return out;
  }, blocks);

  expect(
    failures,
    `mermaid parse failures:\n${failures.map((f) => `  ${f.file} (block ${f.n}): ${f.err}`).join('\n')}`
  ).toEqual([]);
});

// Diagrams are interactive like the maps: wheel zooms the diagram (not the
// page), a reset button is added, and it restores the original view.
test('diagram zooms on wheel and resets', async ({ page, baseURL }) => {
  test.skip(page.viewportSize().width <= 500, 'desktop layout only');
  await page.goto(`${baseURL}/features/pbs/`, { waitUntil: 'networkidle' });
  const box = page.locator('.mermaid').first();
  await box.waitFor({ state: 'visible', timeout: 15000 });
  await box.locator('svg').waitFor({ timeout: 15000 });
  await box.scrollIntoViewIfNeeded();
  await page.waitForTimeout(100);
  const b = await box.boundingBox();
  await page.mouse.move(b.x + b.width / 2, b.y + b.height / 2);
  const y0 = await page.evaluate(() => window.scrollY);
  await page.mouse.wheel(0, -240);
  await page.waitForTimeout(50);
  expect(await page.evaluate(() => window.scrollY)).toBe(y0); // page did not scroll
  let t = await box.locator('svg').evaluate((el) => el.style.transform);
  expect(t).toMatch(/scale\((1\.[1-9]|[2-9])/);

  const reset = box.locator('.zonemap-reset');
  expect(await reset.count()).toBe(1);
  await reset.click();
  t = await box.locator('svg').evaluate((el) => el.style.transform);
  expect(t).toMatch(/scale\(1\)/);
});

// Pie charts are deliberately NOT interactive: the wheel keeps scrolling the
// page (it is not consumed), dragging does not pan, and no reset button is
// added. Rendered with the site's own mermaid build.
test('pie chart is not interactive', async ({ page, baseURL }) => {
  test.skip(page.viewportSize().width <= 500, 'desktop layout only');
  await page.goto(`${baseURL}/features/pbs/`, { waitUntil: 'networkidle' });
  await page.waitForFunction(() => window.mermaid && typeof window.mermaid.run === 'function');

  // Inject a pie chart the same way the page renders its diagrams.
  await page.evaluate(() => {
    const pre = document.createElement('pre');
    pre.className = 'mermaid pie-probe';
    pre.textContent = 'pie title Probe\n"A" : 44\n"B" : 56';
    document.body.appendChild(pre);
    return mermaid.run({ querySelector: '.pie-probe' });
  });
  const box = page.locator('pre.pie-probe');
  await box.locator('svg').waitFor({ timeout: 15000 });
  await box.evaluate((el) => el.scrollIntoView({ block: 'center' }));
  await page.waitForTimeout(100);
  const b = await box.boundingBox();
  const cx = b.x + b.width / 2, cy = b.y + b.height / 2;

  // Wheel over the pie scrolls the PAGE (the zoom handler did not consume it).
  const y0 = await page.evaluate(() => window.scrollY);
  await page.mouse.move(cx, cy);
  await page.mouse.wheel(0, -240);
  await page.waitForTimeout(80);
  expect(await page.evaluate(() => window.scrollY)).not.toBe(y0);
  expect(await box.locator('svg').evaluate((el) => el.style.transform)).toBe('');

  // Dragging over the pie does not pan it.
  await box.evaluate((el) => el.scrollIntoView({ block: 'center' }));
  const b2 = await box.boundingBox();
  await page.mouse.move(b2.x + b2.width / 2, b2.y + b2.height / 2);
  await page.mouse.down();
  await page.mouse.move(b2.x + b2.width / 2 + 80, b2.y + b2.height / 2 + 40, { steps: 4 });
  await page.mouse.up();
  expect(await box.locator('svg').evaluate((el) => el.style.transform)).toBe('');

  // No reset button is added to a pie chart.
  expect(await box.locator('.zonemap-reset').count()).toBe(0);
});
