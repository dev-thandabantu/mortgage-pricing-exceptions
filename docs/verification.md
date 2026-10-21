# Verification

- Build: no warnings/errors. `dotnet test`: 23 passing tests, using file-backed SQLite for HTTP/persistence scenarios.
- Browser: Chromium exercised create, amendment, a conflicting stale approval, refresh, approval, history and downstream discount; checked text escaping and a 390px layout without horizontal overflow.
- Review demonstration: three regression checks pass unchanged; each fails on its corresponding faulty patch; a comment-only control passes. See `review-demo/results.json`.
- Fresh Git checkout: restore, Release build and all 23 tests passed.
- Extracted source zip: all 23 tests passed; the documented PowerShell HTTP scenario passed against its Release build.
- Final browser pass against the extracted zip: creation, amendment, stale-approval conflict, refresh, approval, history and downstream read passed; desktop/mobile screenshots were inspected.
- Production startup explicitly rejected demo authentication.
- GitHub Actions on Ubuntu passed for application commit `5b6851d`. Subsequent packaging changes update documentation and the optional check scripts; [CI history](https://github.com/dev-thandabantu/mortgage-pricing-exceptions/actions) records each pushed revision.

## Optional browser check

`scripts/browser-smoke.cjs` requires Node and Puppeteer only for browser testing. Set `PUPPETEER_MODULE` to a resolvable module/path and optionally `CHROME_PATH` to an installed Chromium executable. With a fresh local service, run `node scripts/browser-smoke.cjs`. `BASE_URL` defaults to `http://127.0.0.1:5027`; `APPLICATION_ID` defaults to `app-1001`. The script leaves synthetic requests in its database and writes screenshots to ignored `artifacts/`.

## Optional review check

`python scripts/check-review-demo.py` needs Python 3 and .NET 10. It copies the source into ignored `artifacts/`, runs the checks against the baseline and each mutation, and records real outcomes. It never changes the application source. This is illustrative verification of proposed findings, not evidence of broad model accuracy.

## PR review follow-up

The fresh-chat review supplied by Brighton reported no material findings and 23/23 tests. The implementation-session review reproduced two UI issues in `350c12d`: failed saves erased drafts/comments, and delayed reads could restore an older selection. Four controlled Chromium checks failed before the fixes and pass afterward. The full browser workflow also passes. The API/domain code was unchanged.

Reproduce with `node scripts/browser-review.cjs`, using the same Puppeteer/Chromium environment as the smoke check. It defaults to port 5030 and an isolated database with app-1001/app-1002 available. It injects temporary save failures and delays read responses; synthetic pending fixtures remain in that database. Final fresh-chat review and acceptance are still pending.
