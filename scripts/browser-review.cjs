const assert = require('node:assert/strict');
const fs = require('node:fs');
const puppeteer = require(process.env.PUPPETEER_MODULE || 'puppeteer');
const base = process.env.BASE_URL || 'http://127.0.0.1:5030';
(async () => {
  const browser = await puppeteer.launch({ headless: true, executablePath: process.env.CHROME_PATH || undefined });
  const page = await browser.newPage();
  const checks = [], errors = [];
  let fault = null, heldRead = null;
  const record = (name, passed, actual) => checks.push({ name, passed, actual });
  const holdNextRead = path => new Promise(resolve => { heldRead = { path, resolve }; });
  const capturedWithin = promise => Promise.race([promise, new Promise((_, reject) => setTimeout(() => reject(new Error('Read interception timed out')), 5000))]);
  page.on('pageerror', error => errors.push(error.message));
  await page.setRequestInterception(true);
  page.on('request', request => {
    const path = new URL(request.url()).pathname;
    if (fault && request.method() === fault.method && path === fault.path) {
      const detail = fault.detail;
      fault = null;
      return request.respond({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail }) });
    }
    if (heldRead && request.method() === 'GET' && path === heldRead.path) {
      const resolve = heldRead.resolve;
      heldRead = null;
      resolve(request);
      return;
    }
    request.continue();
  });
  try {
    await page.goto(base, { waitUntil: 'networkidle0' });
    const requests = await page.evaluate(async () => {
      const headers = { 'Content-Type': 'application/json', 'X-User-Id': 'rm-alex' };
      const list = await (await fetch('/pricing-exceptions', { headers })).json();
      const result = [];
      for (const applicationId of ['app-1001', 'app-1002']) {
        let request = list.find(value => value.applicationId === applicationId && value.status === 'Pending');
        if (!request) {
          const response = await fetch('/pricing-exceptions', { method: 'POST', headers: { ...headers, 'Idempotency-Key': crypto.randomUUID() }, body: JSON.stringify({ applicationId, discountBps: 25, reason: 'Review fixture' }) });
          if (response.status !== 201) throw new Error('Use an isolated database with app-1001/app-1002 available.');
          request = await response.json();
        }
        result.push(request);
      }
      return result;
    });
    const [a, b] = requests;
    const row = request => `.request-row[data-id="${request.id}"]`;
    const detailIs = id => page.waitForFunction(value => document.querySelector('#detail-application').textContent === value, {}, id);
    await page.reload({ waitUntil: 'networkidle0' });
    await page.click(row(a));
    await detailIs(a.applicationId);
    await page.$eval('#amend-discount', element => element.value = '45');
    await page.$eval('#amend-reason', element => element.value = 'Keep this amendment draft');
    fault = { method: 'PATCH', path: `/pricing-exceptions/${a.id}`, detail: 'Injected amendment outage' };
    await page.click('#amend-form button');
    await page.waitForFunction(() => document.querySelector('#message').textContent === 'Injected amendment outage');
    const draft = await page.evaluate(() => ({ discount: document.querySelector('#amend-discount').value, reason: document.querySelector('#amend-reason').value }));
    record('Failed amendment preserves the entered draft', draft.discount === '45' && draft.reason === 'Keep this amendment draft', draft);

    await page.select('#identity', 'reviewer-jules');
    await page.waitForNetworkIdle({ idleTime: 100 });
    await page.$eval('#comment', element => element.value = 'Keep this decision rationale');
    fault = { method: 'POST', path: `/pricing-exceptions/${a.id}/decisions`, detail: 'Injected decision outage' };
    await page.click('#decision-form button[value="Approved"]');
    await page.waitForFunction(() => document.querySelector('#message').textContent === 'Injected decision outage');
    const comment = await page.$eval('#comment', element => element.value);
    record('Failed decision preserves its comment', comment === 'Keep this decision rationale', comment);

    const capturedA = holdNextRead(`/pricing-exceptions/${a.id}`);
    await page.click(row(a));
    const delayedA = await capturedWithin(capturedA);
    await page.click(row(b));
    await detailIs(b.applicationId);
    await delayedA.continue();
    await page.waitForNetworkIdle({ idleTime: 100 });
    const latest = await page.$eval('#detail-application', element => element.textContent);
    record('Older detail response cannot replace the latest selection', latest === b.applicationId, latest);

    await page.click(row(b));
    await detailIs(b.applicationId);
    const capturedList = holdNextRead('/pricing-exceptions');
    await page.click('#refresh');
    const delayedList = await capturedWithin(capturedList);
    await page.click(row(a));
    await detailIs(a.applicationId);
    await delayedList.continue();
    await page.waitForNetworkIdle({ idleTime: 100 });
    const refreshed = await page.$eval('#detail-application', element => element.textContent);
    record('Older refresh cannot undo a newer selection', refreshed === a.applicationId, refreshed);
    assert.deepEqual(errors, []);
    const result = { checks, pageErrors: errors };
    fs.mkdirSync('artifacts', { recursive: true });
    fs.writeFileSync(process.env.REVIEW_RESULTS || 'artifacts/browser-review.json', JSON.stringify(result, null, 2));
    console.log(JSON.stringify(result, null, 2));
    if (checks.some(check => !check.passed)) process.exitCode = 1;
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
