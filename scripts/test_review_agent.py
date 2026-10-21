"""Offline checks of the runner's trust boundaries; no model access."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import review_agent as agent

class ReviewAgentTests(unittest.TestCase):
    def setUp(self):
        self.path = "src/PricingExceptions.Api/Domain.cs"
        self.changes = {self.path: {2}}
        self.finding = {"path": self.path, "line": 2, "severity": "high", "title": "Lost check", "explanation": "Stale approval succeeds."}

    def value(self, findings=None):
        return {"summary": "Reviewed", "findings": [self.finding] if findings is None else findings}

    def test_valid_finding_on_added_line(self):
        self.assertEqual(agent.validate(self.value(), self.changes), self.value())

    def test_non_string_severity_is_rejected(self):
        self.finding["severity"] = []
        with self.assertRaises(ValueError):
            agent.validate(self.value(), self.changes)

    def test_context_is_never_silently_truncated(self):
        with self.assertRaises(ValueError):
            agent.bundle({self.path: "old"}, {self.path: "x" * agent.MAX_INPUT})

    def test_out_of_scope_path_is_rejected(self):
        self.finding["path"] = "../../secrets"
        with self.assertRaises(ValueError):
            agent.validate(self.value(), self.changes)

    def test_context_line_is_rejected(self):
        self.finding["line"] = 1
        with self.assertRaises(ValueError):
            agent.validate(self.value(), self.changes)

    def test_boolean_is_not_a_line_number(self):
        self.finding["line"] = True
        with self.assertRaises(ValueError):
            agent.validate(self.value(), self.changes)

    def test_unstructured_response_is_rejected(self):
        with self.assertRaises(ValueError):
            agent.validate({"findings": []}, self.changes)

    def test_bundle_contains_source_and_diff_only(self):
        prompt, changed = agent.bundle({self.path: "one\ntwo\n"}, {self.path: "one\nthree\n"})
        self.assertEqual(changed, self.changes)
        self.assertIn("2: three", prompt)
        self.assertNotIn("expectsFault", prompt)
        self.assertNotIn("case-a", prompt)
        self.assertNotIn("expectedFailed", prompt)

    def test_timeout_is_an_error_not_a_clean_review(self):
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(agent, "invoke_cli", side_effect=subprocess.TimeoutExpired("codex", 10)):
                result = agent.call_model("input", self.changes, Path(directory) / "call", 10, ["codex"])
            self.assertEqual(result["status"], "error")
            self.assertEqual(agent.score(result, False), "operational-error")

    def test_malformed_json_is_an_operational_error(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "call"
            def fake(*args, **kwargs):
                (target / "response.json").write_text("not json", encoding="utf-8")
                return subprocess.CompletedProcess([], 0, "", "")
            with patch.object(agent, "invoke_cli", side_effect=fake):
                result = agent.call_model("input", self.changes, target, 10, ["codex"])
            self.assertEqual(agent.score(result, True), "operational-error")

    def test_clean_control_false_positive_is_not_detection(self):
        result = {"status": "reviewed", "review": self.value()}
        self.assertEqual(agent.score(result, False), "false-positive")

    def test_missing_fault_and_quiet_control_are_distinct(self):
        result = {"status": "reviewed", "review": self.value([])}
        self.assertEqual(agent.score(result, True), "missed")
        self.assertEqual(agent.score(result, False), "quiet-control")

if __name__ == "__main__":
    unittest.main()
