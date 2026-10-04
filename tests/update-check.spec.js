// update-check.js: while a page is open, the site re-fetches the current
// page (no-store) and compares the pristine <main> against a snapshot
// taken while the document was still parsing. A difference means the site
// was redeployed → a toast appears, bottom right; clicking it reloads.
const { test, expect } = require('@playwright/test');

const PAGE = '/features/items/';

// make the server pretend the deployed page changed: same response for
// navigations (sec-fetch-dest: document), modified <main> for the
// no-store re-fetch the checker does (sec-fetch-dest: empty)
async function pretendRedeploy(page, baseURL) {
  await page.route(`${baseURL}${PAGE}`, async (route) => {
    const isNavigation = route.request().headers()['sec-fetch-dest'] === 'document';
    if (isNavigation) return route.continue();
    const response = await route.fetch();
    const html = (await response.text()).replace('</main>', '<p data-deployed="newer">x</p></main>');
    return route.fulfill({ response, body: html });
  });
}

test('no toast when the deployed page is unchanged', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}${PAGE}`, { waitUntil: 'load' });
  // the first check runs ~2 s after load
  await page.waitForTimeout(4000);
  expect(await page.locator('.update-toast').count()).toBe(0);
});

test('toast appears when the deployed page changed, and clicking it refreshes', async ({ page, baseURL, context }) => {
  await page.goto(`${baseURL}${PAGE}`, { waitUntil: 'load' });
  await pretendRedeploy(page, baseURL);

  const toast = page.locator('.update-toast');
  await toast.waitFor({ state: 'visible', timeout: 15000 });
  expect((await toast.textContent()).toLowerCase()).toContain('a new update is available');

  // clicking reloads; once the loaded page matches the deployment again,
  // the toast goes away
  await toast.click();
  await page.waitForLoadState('load');
  await context.unroute(`${baseURL}${PAGE}`);
  await page.waitForTimeout(4000);
  expect(await page.locator('.update-toast').count()).toBe(0);
});
