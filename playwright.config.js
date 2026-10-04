// Playwright configuration for the wiki browser tests (tests/).
//
// The tests run against a locally built site: `make build && npm test`
// (CI builds the site first, see .github/workflows/wiki.yml). A small static
// server (tests/serve.js) serves public/ — no extra tooling needed.
//
// The Playwright-bundled Chromium is used by default; to use a system browser
// instead (skips the download), set CHROMIUM_PATH, e.g.
//   CHROMIUM_PATH=/usr/bin/chromium npm test

const { defineConfig } = require('@playwright/test');

const port = process.env.WIKI_TEST_PORT || '4173';

module.exports = defineConfig({
  testDir: 'tests',
  timeout: 180 * 1000,
  retries: 0,
  workers: 1, // single static server; the mermaid suite is CPU-heavy
  reporter: 'list',
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    launchOptions: process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {},
  },
  projects: [
    { name: 'desktop', use: { viewport: { width: 1280, height: 720 } } },
    // phone-sized; isMobile/hasTouch emulate a real handset (viewport meta,
    // touch events). Mobile-only tests skip on the desktop project.
    {
      name: 'mobile',
      use: {
        viewport: { width: 390, height: 844 },
        deviceScaleFactor: 2,
        isMobile: true,
        hasTouch: true,
      },
    },
  ],
  webServer: {
    command: `node tests/serve.js public ${port}`,
    url: `http://127.0.0.1:${port}/`,
    reuseExistingServer: true,
    timeout: 15 * 1000,
  },
});
