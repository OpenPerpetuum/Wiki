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
