"""Exercise propagation, transport failures and the shared timeout without NuGet."""

import os
from pathlib import Path
import subprocess
import tempfile
import time
import unittest


SCRIPT = Path(__file__).with_name("verify-published-packages.sh")
VERSION = "10.0.56-beta"
IDS = [
    "wow2.sdk.backend.beta",
    "wow2.sdk.backend.beta.data.abstractions",
    "wow2.sdk.backend.beta.data.migrations.cli",
    "wow2.sdk.backend.beta.testing",
    "wow2.sdk.backend.beta.testing.data",
    "wow2.sdk.backend.beta.testing.integrations",
    "wow2.sdk.backend.beta.testing.messaging",
]


class PublicationVerifierTests(unittest.TestCase):
    def run_probe(self, mode, timeout="3", version=VERSION):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            curl = root / "curl"
            curl.write_text("""#!/usr/bin/env python3
import os, pathlib, sys, time
root = pathlib.Path(os.environ['PROBE_STATE'])
package = sys.argv[-1].split('/')[-3]
counter = root / package
attempt = int(counter.read_text()) + 1 if counter.exists() else 1
counter.write_text(str(attempt))
with (root / 'requests').open('a') as log:
    log.write(package + '\\n')
mode = os.environ['PROBE_MODE']
if mode == 'slow':
    time.sleep(float(sys.argv[sys.argv.index('--max-time') + 1]))
    print('000', end='')
    sys.exit(28)
if mode == 'transport' and attempt == 1:
    print('000', end='')
    sys.exit(6)
delayed = package.endswith(('abstractions', 'messaging')) and attempt == 1
print('404' if mode == 'missing' or (mode == 'delayed' and delayed) else '200', end='')
""")
            curl.chmod(0o755)
            start = time.monotonic()
            result = subprocess.run(
                ["bash", str(SCRIPT), version, timeout, "1"],
                env={**os.environ, "PATH": f"{root}:{os.environ['PATH']}",
                     "PROBE_STATE": str(root), "PROBE_MODE": mode},
                text=True, capture_output=True, timeout=10,
            )
            requests = root / "requests"
            return result, requests.read_text().splitlines() if requests.exists() else [], time.monotonic() - start

    def test_all_available(self):
        result, requests, _ = self.run_probe("available")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(requests, IDS)

    def test_only_missing_packages_are_polled_again(self):
        result, requests, _ = self.run_probe("delayed", timeout="5")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(requests, IDS + [IDS[1], IDS[-1]])
        self.assertIn("Pending packages: " + IDS[1] + " " + IDS[-1], result.stdout)

    def test_transport_errors_can_recover(self):
        result, requests, _ = self.run_probe("transport", timeout="5")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(requests, IDS + IDS)
        self.assertIn("HTTP 000, curl 6", result.stdout)

    def test_timeout_reports_every_unverified_package(self):
        result, _, elapsed = self.run_probe("missing", timeout="1")
        self.assertEqual(result.returncode, 1)
        for package in IDS:
            self.assertIn("Unverified: " + package, result.stderr)
        self.assertLess(elapsed, 3)

    def test_slow_request_cannot_get_a_new_per_package_budget(self):
        result, requests, elapsed = self.run_probe("slow", timeout="1")
        self.assertEqual(result.returncode, 1)
        self.assertEqual(requests, [IDS[0]])
        self.assertLess(elapsed, 3)
        for package in IDS:
            self.assertIn("Unverified: " + package, result.stderr)

    def test_invalid_arguments_fail_without_network(self):
        for version, timeout in [("10.0.56", "3"), (VERSION, "0"), (VERSION, "-1")]:
            with self.subTest(version=version, timeout=timeout):
                result, requests, _ = self.run_probe("available", timeout, version)
                self.assertEqual(result.returncode, 2)
                self.assertEqual(requests, [])


if __name__ == "__main__":
    unittest.main()
