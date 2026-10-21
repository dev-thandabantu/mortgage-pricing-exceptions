# Verification

- Build: no warnings/errors. `dotnet test`: 23 passing tests, using file-backed SQLite for HTTP/persistence scenarios.
- Browser: Chromium exercised create, amendment, a conflicting stale approval, refresh, approval, history and downstream discount; checked text escaping and a 390px layout without horizontal overflow.
- Review demonstration: three regression checks pass unchanged; each fails on its corresponding faulty patch; a comment-only control passes. See `review-demo/results.json`.
- Clean checkout, source zip and final CI verification are recorded below when completed.

## Optional browser check

`scripts/browser-smoke.cjs` requires Node and Puppeteer only for browser testing. Set `PUPPETEER_MODULE` to a resolvable module/path and optionally `CHROME_PATH` to an installed Chromium executable. With a fresh local service, run `node scripts/browser-smoke.cjs`. `BASE_URL` defaults to `http://127.0.0.1:5027`; `APPLICATION_ID` defaults to `app-1001`. The script leaves synthetic requests in its database and writes screenshots to ignored `artifacts/`.

## Optional review check

`python scripts/check-review-demo.py` needs Python 3 and .NET 10. It copies the source into ignored `artifacts/`, runs the checks against the baseline and each mutation, and records real outcomes. It never changes the application source. This is illustrative verification of proposed findings, not evidence of broad model accuracy.
