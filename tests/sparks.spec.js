// The sparks page: the generated family overview graph (boxes link to the
// per-family sections) and the per-spark cards, plus the hand-written prose
// (unlock rules, the connection tree, switching) that the generator carries.
const { test, expect } = require("@playwright/test");

const SPARKS = "/features/sparks/";

test.describe.configure({ mode: "serial" });

test("sparks page: family boxes link to per-family card sections", async ({ page, baseURL }) => {
  const errors = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto(`${baseURL}${SPARKS}`, { waitUntil: "networkidle" });
  expect(errors).toEqual([]);

  const h1 = page.locator("main h1");
  expect(await h1.count()).toBe(1);
  expect(await h1.textContent()).toBe("Sparks");

  // the overview graph: one box per family, each a real link to #family-<slug>
  const boxes = page.locator(".sparkfam-wrap svg a[href^='#family-']");
  expect(await boxes.count()).toBe(6);
  const hrefs = [];
  for (const b of await boxes.all()) hrefs.push(await b.getAttribute("href"));
  for (const h of ["#family-special", "#family-tm", "#family-ics", "#family-asi", "#family-syndicate", "#family-limited"]) {
    expect(hrefs).toContain(h);
  }

  // the boxes link to the #family-* anchors; each family section exists and
  // carries its cards (47 total: 9/9/9/9/5/6)
  // card counts per family, in the fixed page order (DOM order)
  const per = await page.evaluate((ids) => ids.map((id) => {
    const a = document.getElementById(id);
    if (!a) return null; // anchor missing entirely
    let el = a.parentElement.nextElementSibling; // past the <p> Zola wraps in
    while (el && !el.classList.contains("ext-cards")) el = el.nextElementSibling;
    return el ? el.querySelectorAll(".ext-card").length : -1;
  }), ["family-special", "family-tm", "family-ics", "family-asi", "family-syndicate", "family-limited"]);
  expect(per, `card counts per family (in page order)`).toEqual([9, 9, 9, 9, 5, 6]);

  // the family sections: 6 headings with counts, 47 cards total
  const famHeads = page.locator("main h3");
  expect(await famHeads.count()).toBe(6);
  expect(await page.locator("main .ext-card").count()).toBe(47);

  // card values are emphasized: NIC prices green, standing/bonus amber
  expect(await page.locator("main .ext-val-price").count()).toBeGreaterThan(20);
  expect(await page.locator("main .ext-val-bonus").count()).toBeGreaterThan(10);

  // the old vertical connection tree is gone (family cards replaced it)
  expect(await page.locator("img[src='/sparks-tree.svg']").count()).toBe(0);
  expect(await page.locator("#tree").count()).toBe(0);

  // clicking a family box scrolls to the family section (the anchor keeps the
  // heading below the sticky topbar — scroll-margin-top on [id] elements)
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.locator(".sparkfam-wrap svg a[href='#family-syndicate']").click();
  await page.waitForTimeout(400);
  const pos = await page.evaluate(() => {
    const el = document.getElementById("family-syndicate");
    if (!el) return -999;
    const r = el.getBoundingClientRect();
    return r.top;
  });
  // below the sticky topbar (~112px) with room to spare
  expect(pos).toBeGreaterThan(110);
  expect(pos).toBeLessThan(400);
});
