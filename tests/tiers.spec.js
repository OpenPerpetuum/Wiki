// The tier system must be readable by someone who has never seen it:
// item pages show T1–T4 in the Tier row, a "Tier line" row linking the whole
// standard → named1 → named2 → named3 chain (current item bold), and the
// production page explains the rules in its tier-progression section.
const { test, expect } = require('@playwright/test');

const DRILLER = {
  t1: '/content/items/standard-small-driller/',
  t2: '/content/items/named1-small-driller/',
  t3: '/content/items/named2-small-driller/',
  t4: '/content/items/named3-small-driller/',
};

async function tierRow(page, baseURL, path) {
  await page.goto(baseURL + path, { waitUntil: 'domcontentloaded' });
  const table = page.locator('table').first(); // the identity table
  const row = (label) => table
    .locator('tr', { has: page.locator('td:nth-child(1)', { hasText: new RegExp(`^${label}$`) }) })
    .locator('td').nth(1);
  return { tier: row('Tier'), line: row('Tier line') };
}

test('the T3 item page shows its tier and the full T1 → T4 line', async ({ page, baseURL }) => {
  const { tier, line } = await tierRow(page, baseURL, DRILLER.t3);
  expect(await tier.innerText()).toBe('T3');
  expect(await line.count()).toBe(1);
  const text = await line.innerText();
  // the whole chain, in order
  expect(text).toContain('Standard small miner module (T1)');
  expect(text).toContain('Biroter 5050 small miner module (T2)');
  expect(text).toContain('Sublimator Low-D small miner module (T3)');
  expect(text).toContain('Scraper-990 small miner module (T4)');
  const links = line.locator('a');
  expect(await links.count()).toBe(3); // the current item is plain bold text
  expect(await links.nth(0).getAttribute('href')).toBe(DRILLER.t1);
  expect(await links.nth(1).getAttribute('href')).toBe(DRILLER.t2);
  expect(await links.nth(2).getAttribute('href')).toBe(DRILLER.t4);
  expect(await line.locator('strong').count()).toBe(1);
});

test('the T1 page shows the same chain from the other end', async ({ page, baseURL }) => {
  const { tier, line } = await tierRow(page, baseURL, DRILLER.t1);
  expect(await tier.innerText()).toBe('T1');
  expect(await line.locator('a').count()).toBe(3);
  expect(await line.locator('strong').innerText()).toContain('Standard small miner module');
});

test('the production page explains the tier progression rules', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/features/production/`, { waitUntil: 'domcontentloaded' });
  await expect(page.locator('#tier-progression')).toHaveCount(1);
  // the section's content: from its heading up to the next h2
  const text = await page.evaluate(() => {
    const h = document.querySelector('#tier-progression');
    const out = [];
    for (let el = h.nextElementSibling; el && el.tagName !== 'H2'; el = el.nextElementSibling)
      out.push(el.textContent);
    return out.join('\n');
  });
  expect(text).toMatch(/no way to make a higher tier without the research/i);
  expect(text).toMatch(/specimen of the previous tier/i);
  expect(text).toMatch(/can never/i); // "a T1 item can never be turned in for a T3"
});
