# Optional bounded review automation

This is an SDLC demonstration, separate from mortgage decision-making. The application runs without Python, model access or paid services. Live review uses Python 3.10+ and an installed, authenticated Codex CLI; verified here with CLI 0.162.1. It sends the supplied service source and diffs to OpenAI. Run it only on source approved for that account/provider. Normal CI runs offline runner tests only.

## Run

```sh
codex login
python scripts/review_agent.py --evaluate
python scripts/review_agent.py --base submission-final --head HEAD
python -m unittest discover -s scripts -p test_review_agent.py -v
```

`--evaluate` applies four authored changes in memory, not in the application. Each gets a fresh invocation; case names, expected outcomes and regression test names are kept out of the prompt. `--base` reviews tracked service C#/JavaScript changes between Git commits, with the proposed source as context. It ignores other file types; a change with no supported source diff is explicitly skipped, not approved. Deletion-only changes need manual review because this runner requires findings on added lines. It rejects source context above 100 KB rather than truncating it. It is a deliberately small reviewer, not a general repository audit.

Output defaults to a new ignored `artifacts/agent-review-<UTC>/` directory. `--output` selects another new directory. Each call records its input, input SHA-256, CLI JSON events, stderr and final response locally. The report includes source commit, CLI version, elapsed time, available token usage and outcomes. Inputs and outputs may contain source; keep them private. No account credentials are read into the input bundle or reports. The CLI handles its own authentication.

`--timeout` bounds each call (default 180 seconds, range 10-600); there are no retries. A failed invocation, timeout, invalid JSON or invalid finding location is an operational error. Exit 0 means a completed review/evaluation (or explicit no-source-change skip), **not approval to merge**; exit 1 indicates a model-call error, missed fixture or control false positive; exit 2 is a setup/input failure.

## Controls

The host constructs the diff and numbered source, supplies domain invariants and invokes `codex exec` with structured JSON output, read-only sandboxing and an ephemeral session. Shell, apps, plugins and multi-agent features are disabled for that invocation; user configuration is ignored. The runner does not execute model suggestions, edit source, post GitHub reviews or merge. It validates the response shape, severity, non-empty finding text, allowed path and added-line location. Source comments are treated as untrusted data in the prompt. Schema/location checks constrain output; they do not prove its reasoning is correct.

Independent executable checks use only authored fixture/test definitions, never model-generated commands:

```sh
python scripts/check-review-demo.py --output artifacts/automation-regressions.json
```

This copies application/tests into an isolated ignored directory. Three targeted tests pass unchanged, then each fails for its corresponding fault; the comment-only control passes. It does not alter application source.

## Observed run

[Actual model output](review-agent/results.json), [independent regression outcomes](review-agent/regressions.json). Source: `5a8ba5d`; CLI 0.162.1; 21 October 2026.

| Change | Model result | Executable regression |
|---|---|---|
| Disable expected-version guard | Located the stale-approval defect | Fails (baseline passes) |
| Disable self-decision guard | Located dual-role self-approval | Fails (baseline passes) |
| Disable replay fingerprint comparison | Located different-fields replay | Fails (baseline passes) |
| Reword a comment | No findings | Passes |

The implementation-session inspection checked the three explanations against those failures. Automated scoring measures whether a schema-valid finding lands on the mutated line. It does not independently establish semantic correctness. These are four obvious, authored examples, not a blind benchmark or evidence of general accuracy. No misses, false positives or operational errors occurred in this run; the runner reports each separately when they do. The CLI selected its default model; the captured JSON events did not expose an exact model identity.

Remaining risks include missed defects, plausible false findings, prompt influence from source text, missing context and changes in CLI/provider behavior. Deterministic tests and human review remain the acceptance gate. Live credentials and calls are deliberately absent from CI.

Interface reference: [Codex non-interactive mode](https://learn.chatgpt.com/docs/non-interactive-mode). Recheck CLI support if changing versions.
