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

An independent fresh-session review initially reported no material findings and 23/23 tests. The implementation-session review reproduced two UI issues in `350c12d`: failed saves erased drafts/comments, and delayed reads could restore an older selection. Four controlled Chromium checks failed before the fixes and pass afterward. The full browser workflow also passes. The API/domain code was unchanged.

Reproduce with `node scripts/browser-review.cjs`, using the same Puppeteer/Chromium environment as the smoke check. It defaults to port 5030 and an isolated database with app-1001/app-1002 available. It injects temporary save failures and delays read responses; synthetic pending fixtures remain in that database. Final fresh-chat review passed at `e461347` (23 tests, full browser workflow and all four UI regression checks). PR #8 was accepted and merged as `5a8ba5d`; its merge tree matches the reviewed source. The automation follow-up separately passed final fresh-session review at `16c0808` and merged through PR #19 as `c69dd7a`; its merge tree also matches the reviewed source.


## Automation follow-up

The optional [live review runner](review-agent.md) detected all three authored faults and returned no findings on the comment-only control. The independently rerun regression harness confirmed the expected baseline/fault/control behavior. Twelve offline Python checks cover output scope, malformed responses, timeout handling and outcome classification; these run in CI without model credentials. The core service source was unchanged by this follow-up.


The final PR #19 review also checked the no-source-change skip and regenerated evaluation-input hashes without rerunning live model calls. The accepted source archive was verified byte-for-byte against `c69dd7a`, then its extracted Release build, 23 .NET tests and 12 offline Python checks passed. Subsequent documentation changes follow a separate PR review; they do not alter those historical code-verification results.
