# AI assistance

Codex helped review the brief, implement the API/tests and prepare examples. Brighton approved scope and design. Verification used the real HTTP pipeline and file-backed SQLite; 21 tests pass, including retries after restart, competing writes and rollback after an injected history-write failure. The first test run exposed an unstable decision id in response deserialization; separate response DTOs fixed it. No AI participates in pricing decisions.
