const assert = require('node:assert/strict');
const fs = require('node:fs');
const puppeteer = require(process.env.PUPPETEER_MODULE || 'puppeteer');
const applicationId = process.env.APPLICATION_ID || 'app-1001';
const base = process.env.BASE_URL || 'http://127.0.0.1:5027';
(async () => {
  const browser = await puppeteer.launch({ headless: true, executablePath: process.env.CHROME_PATH || undefined });
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  try {
    await page.setViewport({ width: 1440, height: 1150, deviceScaleFactor: 1 });
    await page.goto(base, { waitUntil: 'networkidle0' });
    await page.waitForFunction(() => document.querySelectorAll('#identity option').length === 6);
    await page.select('#application', applicationId);
    await page.type('#reason', 'Customer retention <img src=x onerror=alert(1)>');
    await page.click('#create-button');
    await page.waitForFunction(id => document.querySelector('#detail-application').textContent === id, {}, applicationId);
    assert.equal(await page.$eval('#detail-discount', element => element.textContent), '25 bps');
    assert.equal(await page.$eval('#detail-reason', element => element.querySelector('img')), null);
    await page.$eval('#amend-discount', element => element.value = '40');
    await page.$eval('#amend-reason', element => element.value = 'Corrected offer');
    await page.click('#amend-form button');
    await page.waitForFunction(() => document.querySelector('#detail-revision').textContent.startsWith('Revision 2'));
    assert.equal(await page.$$eval('#history li', elements => elements.length), 2);
    await page.select('#identity', 'reviewer-jules');
    await page.waitForFunction(() => !document.querySelector('#decision-form').hidden);
    await page.waitForNetworkIdle({ idleTime: 100 });
    // An RM changes terms while this browser still displays revision 2.
    const requestId = await page.$eval('.request-row', element => element.dataset.id);
    const amended = await page.evaluate(async id => {
      const response = await fetch(`/pricing-exceptions/${id}`, { method: 'PATCH', headers: { 'Content-Type': 'application/json', 'X-User-Id': 'rm-alex' }, body: JSON.stringify({ discountBps: 45, reason: 'Updated before decision', expectedVersion: 2 }) });
      return response.status;
    }, requestId);
    assert.equal(amended, 200);
    await page.click('#decision-form button[value="Approved"]');
    await page.waitForFunction(() => document.querySelector('#message').classList.contains('error'));
    assert.match(await page.$eval('#message', element => element.textContent), /changed/);
    await page.click('#refresh');
    await page.waitForFunction(() => document.querySelector('#detail-revision').textContent.startsWith('Revision 3'));
    await page.click('#decision-form button[value="Approved"]');
    await page.waitForFunction(() => document.querySelector('#status').textContent === 'Approved');
    assert.equal(await page.$$eval('#history li', elements => elements.length), 4);
    const downstream = await page.evaluate(async id => {
      const response = await fetch(`/applications/${id}/approved-discount`, { headers: { 'X-User-Id': 'mortgage-process' } });
      return response.json();
    }, applicationId);
    assert.equal(downstream.discountBps, 45);
    assert.equal(downstream.revisionNumber, 3);
    assert.equal(downstream.requestId, requestId);
    fs.mkdirSync('artifacts', { recursive: true });
    await page.screenshot({ path: 'artifacts/ui-desktop.png', fullPage: true });
    await page.setViewport({ width: 390, height: 844, deviceScaleFactor: 1 });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await page.screenshot({ path: 'artifacts/ui-mobile.png', fullPage: true });
    assert.deepEqual(errors, []);
    console.log('PASS: create, amend, stale review conflict, refresh, approve, history, downstream terms, escaped text and mobile width.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
