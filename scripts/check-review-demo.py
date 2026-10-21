"""Optional review demonstration; requires Python 3 and .NET 10. Never modifies the application."""
from pathlib import Path
import datetime
import json
import shutil
import subprocess
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
cases = json.loads((root / "docs/review-demo/cases.json").read_text(encoding="utf-8"))
stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
work = root / "artifacts" / ("review-check-" + stamp)
work.mkdir(parents=True, exist_ok=False)
for directory in ["src", "tests"]:
    shutil.copytree(root / directory, work / directory, ignore=shutil.ignore_patterns("bin", "obj", "state"))
for name in ["global.json", "PricingExceptions.slnx"]:
    shutil.copyfile(root / name, work / name)
subprocess.run(["dotnet", "restore", "--verbosity", "quiet"], cwd=work, check=True)
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
results = []
def run(label, test):
    completed = subprocess.run(["dotnet", "test", "--no-restore", "--filter", "FullyQualifiedName~" + test,
        "--logger", "trx;LogFileName=" + label + ".trx", "--results-directory", str(work / "results"), "--verbosity", "quiet"],
        cwd=work, text=True, capture_output=True)
    (work / (label + ".log")).write_text(completed.stdout + completed.stderr, encoding="utf-8")
    tree = ET.parse(work / "results" / (label + ".trx"))
    outcomes = [item.get("outcome") for item in tree.findall(".//t:UnitTestResult", ns)]
    if not outcomes or completed.returncode not in (0, 1):
        raise RuntimeError("No usable test result: " + label)
    outcome = "Passed" if all(value == "Passed" for value in outcomes) else "Failed"
    return {"case": label, "test": test, "outcome": outcome, "testsRun": len(outcomes)}
# Every proposed regression check must pass before any mutation is introduced.
for case in cases[:3]:
    baseline = run("baseline-" + case["case"], case["test"])
    assert baseline["outcome"] == "Passed", baseline
    results.append(baseline)
for case in cases:
    target = work / case["file"]
    original = target.read_text(encoding="utf-8")
    assert original.count(case["old"]) == 1, case["case"]
    try:
        target.write_text(original.replace(case["old"], case["new"]), encoding="utf-8")
        result = run(case["case"], case["test"])
        assert result["outcome"] == case["expected"], result
        results.append(result)
        print(case["case"] + ": " + result["outcome"] + " (expected)")
    finally:
        target.write_text(original, encoding="utf-8")
report = {"runUtc": stamp, "method": "Worked demonstration; authored mutations, not a blind benchmark", "results": results}
(root / "docs/review-demo/results.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print("Verified three proposed checks against baseline, three faults and one harmless control.")
