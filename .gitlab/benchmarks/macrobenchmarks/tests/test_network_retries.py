"""Offline regression tests: python3 -m unittest discover -s tests -v."""

import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import zipfile


REPO = Path(__file__).resolve().parents[1]
ARTIFACTS = (
    "linux-monitoring-home-linux-x64",
    "Datadog.Trace.Manual.dll",
    "windows-tracer-home",
    "linux-monitoring-home-linux-arm64",
)


def _gnu_tools_available():
    """Whether jq and the GNU utilities the setup scripts rely on are on PATH."""
    if shutil.which("jq") is None:
        return False
    with tempfile.TemporaryDirectory() as temp:
        source = Path(temp) / "source"
        source.mkdir()
        try:
            # GNU mv supports -T; BSD mv does not.
            subprocess.run(["mv", "-T", str(source), str(Path(temp) / "destination")],
                           capture_output=True, check=True)
            # BSD sed rejects --version and lacks GNU sed's suffix-less -i.
            subprocess.run(["sed", "--version"], capture_output=True, check=True)
        except (OSError, subprocess.CalledProcessError):
            return False
    return True


# The setup-sut.sh tests shell out to jq and GNU mv/sed/timeout; on macOS they
# fail for environmental reasons only. ./test.sh runs the full suite in Linux.
requires_gnu_tools = unittest.skipUnless(
    _gnu_tools_available(),
    "requires GNU coreutils + jq; run ./test.sh for the full Linux run")


class NetworkRetriesTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.work = Path(self.temp.name)
        self.bin = self.work / "bin"
        self.bin.mkdir()
        self.routes = {}
        self.calls = {}
        test = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *_):
                pass

            def do_GET(self):
                index = test.calls.get(self.path, 0)
                test.calls[self.path] = index + 1
                replies = test.routes[self.path]
                status, body, partial = replies[min(index, len(replies) - 1)]
                self.send_response(status)
                self.send_header("Content-Length", str(len(body) + (100 if partial else 0)))
                self.end_headers()
                self.wfile.write(body)
                self.close_connection = True

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(self.server.server_close)
        self.addCleanup(self.server.shutdown)
        self.base_url = f"http://127.0.0.1:{self.server.server_port}"
        # Resolve real binaries now, before the stub dir shadows PATH: the
        # stubs prepend their own directory, so which() inside them would find
        # the stub itself.
        self.curl_bin = shutil.which("curl")
        self.timeout_bin = shutil.which("timeout")
        self.env = dict(os.environ, PATH=f"{self.bin}:{os.environ['PATH']}",
                        TEST_WORK=str(self.work), TEST_SERVER=self.base_url,
                        CI_REPOSITORY_URL="", CI_COMMIT_REF_NAME="master",
                        USE_MASTER_BUILD="1", TEST_BRANCH="", buildId="212448")

    def stub(self, name, code):
        path = self.bin / name
        path.write_text(f"#!{sys.executable}\n" + code)
        path.chmod(0o755)

    def run_bash(self, command):
        return subprocess.run(["bash", "-c", command], cwd=self.work, env=self.env,
                              capture_output=True, timeout=40)

    def curl(self, path="/response", destination="-"):
        return self.run_bash(
            f'source "{REPO}/steps/retry.sh"; '
            f'curl_with_retry "{destination}" "{self.base_url}{path}"')

    def test_partial_metadata_is_retried_without_duplicate_output(self):
        self.routes["/response"] = [(200, b'{"partial":', True),
                                    (200, b'{"complete":true}', False)]
        result = self.curl()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout, b'{"complete":true}')
        self.assertEqual(self.calls["/response"], 2)
        self.assertIn(b"(18)", result.stderr)

    def test_partial_zip_is_retried_without_corrupting_file(self):
        payload = b"PK\x03\x04\x00\xffCOMPLETE"
        self.routes["/response"] = [(200, b"PK\x03\x04PARTIAL", True),
                                    (200, payload, False)]
        result = self.curl(destination="artifact.zip")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual((self.work / "artifact.zip").read_bytes(), payload)
        self.assertEqual(result.stdout, b"")
        self.assertEqual(self.calls["/response"], 2)

    def test_transient_http_errors_are_retried(self):
        for status in (429, 503):
            with self.subTest(status=status):
                path = f"/{status}"
                self.routes[path] = [(status, b"temporary", False), (200, b"ok", False)]
                result = self.curl(path)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertEqual(result.stdout, b"ok")
                self.assertEqual(self.calls[path], 2)

    def test_permanent_http_errors_fail_once_without_body(self):
        for status in (400, 401, 403, 404):
            with self.subTest(status=status):
                path = f"/{status}"
                self.routes[path] = [(status, b"PRIVATE ERROR BODY", False)]
                result = self.curl(path)
                self.assertEqual(result.returncode, 22)
                self.assertEqual(result.stdout, b"")
                self.assertIn(f"HTTP {status}".encode(), result.stderr)
                self.assertNotIn(b"PRIVATE ERROR BODY", result.stderr)
                self.assertEqual(self.calls[path], 1)

    def test_transient_failure_stops_after_four_attempts(self):
        self.routes["/response"] = [(503, b"temporary", False)]
        result = self.curl()
        self.assertEqual(result.returncode, 22)
        self.assertEqual(self.calls["/response"], 4)
        self.assertEqual(result.stdout, b"")

    def setup_stubs(self):
        # Keep curl real, rewriting only the hard-coded ADO host to our server.
        self.stub("curl", f"""
import os, sys
args = [arg.replace('https://dev.azure.com', os.environ['TEST_SERVER']) for arg in sys.argv[1:]]
os.execv({self.curl_bin!r}, ['curl', *args])
""")
        self.stub("sleep", "pass\n")
        self.stub("git", """
import os, sys
from pathlib import Path
if sys.argv[1] == 'clone':
    work = Path(os.environ['TEST_WORK'])
    log = work / 'clone-attempts'
    previous = log.read_text() if log.exists() else ''
    destination = Path(sys.argv[-1])
    log.write_text(previous + str(destination) + '\\n')
    (destination / 'partial-clone').touch()
    if os.environ.get('CLONE_FAILURE') == 'always' or (not previous and os.environ.get('CLONE_FAILURE') == 'once'):
        sys.exit(128)
    project = destination / 'tracer/test/test-applications/throughput/Samples.AspNetCoreSimpleController'
    project.mkdir(parents=True)
    (project / 'Samples.AspNetCoreSimpleController.csproj').write_text('<TargetFramework>net5.0</TargetFramework>')
""")

    def artifact_routes(self, metadata=None):
        for name in ARTIFACTS:
            path = f"/datadoghq/dd-trace-dotnet/_apis/build/builds/212448/artifacts?artifactName={name}&api-version=4.1"
            data = metadata if metadata is not None else {"resource": {"downloadUrl": f"{self.base_url}/{name}"}}
            self.routes[path] = [(200, json.dumps(data).encode(), False)]
            buffer = io.BytesIO()
            with zipfile.ZipFile(buffer, "w") as archive:
                archive.writestr(f"{name}/file", "content")
            self.routes[f"/{name}"] = [(200, buffer.getvalue(), False)]

    @requires_gnu_tools
    def test_setup_retries_clone_and_download_then_installs_artifacts(self):
        self.setup_stubs()
        self.artifact_routes()
        self.env["CLONE_FAILURE"] = "once"
        self.routes[f"/{ARTIFACTS[0]}"].insert(0, (200, b"PK PARTIAL", True))
        result = self.run_bash(f'bash "{REPO}/steps/setup-sut.sh"')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len((self.work / "clone-attempts").read_text().splitlines()), 2)
        self.assertEqual(list(self.work.glob("dd-trace-dotnet.*")), [])
        self.assertTrue((self.work / "dd-trace-dotnet/tracer/tracer-home-linux/file").is_file())
        self.assertEqual(self.calls[f"/{ARTIFACTS[0]}"], 2)

    def test_clone_exhaustion_cleans_partial_directories(self):
        self.setup_stubs()
        self.env["CLONE_FAILURE"] = "always"
        result = self.run_bash(f'bash "{REPO}/steps/setup-sut.sh"')
        self.assertEqual(result.returncode, 128)
        self.assertEqual(len((self.work / "clone-attempts").read_text().splitlines()), 3)
        self.assertEqual(list(self.work.glob("dd-trace-dotnet*")), [])
        self.assertEqual(self.calls, {})

    @requires_gnu_tools
    def test_timed_out_clones_are_retried_and_cleaned(self):
        self.setup_stubs()
        # Shorten the clone timeout through the variables setup-sut.sh honors,
        # instead of rewriting the hard-coded default in the stub.
        self.env["CLONE_TIMEOUT"] = "0.05s"
        self.env["CLONE_KILL_AFTER"] = "0.05s"
        self.stub("timeout", f"""
import os, sys
os.execv({self.timeout_bin!r}, ['timeout', *sys.argv[1:]])
""")
        self.stub("git", """
import sys, time
from pathlib import Path
Path(sys.argv[-1], 'partial').touch()
time.sleep(10)
""")
        result = self.run_bash(f'bash "{REPO}/steps/setup-sut.sh"')
        self.assertEqual(result.returncode, 124)
        self.assertIn(b"Attempt 1 failed (exit 124); retrying", result.stderr)
        self.assertIn(b"Attempt 2 failed (exit 124); retrying", result.stderr)
        self.assertNotIn(b"Attempt 3 failed (exit 124); retrying", result.stderr)
        self.assertEqual(list(self.work.glob("dd-trace-dotnet*")), [])

    def test_existing_clone_is_preserved(self):
        self.setup_stubs()
        existing = self.work / "dd-trace-dotnet"
        existing.mkdir()
        (existing / "keep").touch()
        result = self.run_bash(f'bash "{REPO}/steps/setup-sut.sh"')
        self.assertNotEqual(result.returncode, 0)
        self.assertTrue((existing / "keep").exists())
        self.assertFalse((self.work / "clone-attempts").exists())

    @requires_gnu_tools
    def test_null_download_url_fails_without_retry_or_download(self):
        self.setup_stubs()
        self.artifact_routes(metadata={"resource": {"downloadUrl": None}})
        result = self.run_bash(f'bash "{REPO}/steps/setup-sut.sh"')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b"Failed to get download URL", result.stdout)
        self.assertEqual(sum(self.calls.values()), 1)

    @requires_gnu_tools
    def test_setup_download_failure_never_logs_binary_body(self):
        self.setup_stubs()
        self.artifact_routes()
        self.routes[f"/{ARTIFACTS[0]}"] = [(200, b"\x00\xffBINARY_RESPONSE" * 4096, True)]
        result = self.run_bash(f'bash "{REPO}/steps/setup-sut.sh"')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b"Failed to download artifact", result.stdout)
        self.assertNotIn(b"BINARY_RESPONSE", result.stdout + result.stderr)
        self.assertEqual(self.calls[f"/{ARTIFACTS[0]}"], 4)

    def test_pipeline_api_failure_exits_before_polling(self):
        self.setup_stubs()
        self.routes["/datadoghq/dd-trace-dotnet/_apis/build/builds?queryOrder=startTimeDescending&api-version=7.1-preview.5"] = [(404, b"missing", False)]
        result = self.run_bash(f'bash "{REPO}/../shared/wait-for-pipeline.sh"')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b"Failed to get builds from Azure DevOps", result.stderr)
        self.assertEqual(sum(self.calls.values()), 1)

    def pipeline_routes(self):
        builds_path = "/datadoghq/dd-trace-dotnet/_apis/build/builds?queryOrder=startTimeDescending&api-version=7.1-preview.5"
        build = {"id": 212448, "definition": {"name": "consolidated-pipeline"},
                 "sourceBranch": "refs/heads/master"}
        self.routes[builds_path] = [(200, json.dumps({"value": [build]}).encode(), False)]
        timeline_path = "/datadoghq/dd-trace-dotnet/_apis/build/builds/212448/timeline?api-version=7.0"
        stages = ("package_linux", "package_arm64", "build_windows_tracer",
                  "generate_variables", "merge_commit_id")
        records = [{"name": stage, "state": "completed", "result": "succeeded"}
                   for stage in stages]
        self.routes[timeline_path] = [(200, json.dumps({"records": records}).encode(), False)]
        # The production script writes build-id.txt in its working directory.
        (self.work / "pipeline").mkdir()
        return builds_path, timeline_path

    def test_pipeline_retries_build_and_timeline_reads(self):
        self.setup_stubs()
        builds_path, timeline_path = self.pipeline_routes()
        self.routes[builds_path].insert(0, (503, b"temporary", False))
        self.routes[timeline_path].insert(0, (200, b'{"partial":', True))
        result = self.run_bash(f'cd pipeline; bash "{REPO}/../shared/wait-for-pipeline.sh"')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.calls[builds_path], 2)
        self.assertEqual(self.calls[timeline_path], 6)
        self.assertEqual((self.work / "pipeline/build-id.txt").read_text(), "export buildId=212448\n")

    def test_missing_timeline_exits_without_polling_again(self):
        self.setup_stubs()
        _, timeline_path = self.pipeline_routes()
        self.routes[timeline_path] = [(404, b"missing", False)]
        result = self.run_bash(f'cd pipeline; bash "{REPO}/../shared/wait-for-pipeline.sh"')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b"Failed to get timeline for build 212448", result.stderr)
        self.assertEqual(self.calls[timeline_path], 1)
        self.assertFalse((self.work / "pipeline/build-id.txt").exists())

    def test_missing_build_exits_after_one_successful_request(self):
        self.setup_stubs()
        builds_path, _ = self.pipeline_routes()
        self.routes[builds_path] = [(200, b'{"value":[]}', False)]
        result = self.run_bash(f'cd pipeline; bash "{REPO}/../shared/wait-for-pipeline.sh"')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b"This branch does not have a build related to it", result.stdout)
        self.assertEqual(sum(self.calls.values()), 1)

    def test_s3_uses_bounded_native_retries_and_keeps_optional_results(self):
        self.stub("bp-infra", "pass\n")
        self.stub("aws", """
import json, os, sys
from pathlib import Path
Path(os.environ['TEST_WORK'], 'aws-call').write_text(json.dumps({
    'args': sys.argv[1:], 'mode': os.environ.get('AWS_RETRY_MODE'),
    'attempts': os.environ.get('AWS_MAX_ATTEMPTS')}))
print('fatal error: 404 Not Found', file=sys.stderr)
sys.exit(1)
""")
        self.env["AWS_REGION"] = "us-east-1"
        result = self.run_bash(f'bash "{REPO}/steps/fetch-windows-results.sh"')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(b"WARNING: Could not download benchmark artifacts", result.stdout)
        call = json.loads((self.work / "aws-call").read_text())
        self.assertEqual(call["mode"], "standard")
        self.assertEqual(call["attempts"], "4")
        self.assertEqual(call["args"][-4:], ["--cli-connect-timeout", "15", "--cli-read-timeout", "60"])


if __name__ == "__main__":
    unittest.main()
