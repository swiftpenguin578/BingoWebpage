#!/usr/bin/env python3
"""Focused refusal tests plus one freshly owned /usr/bin/python3 HTTP child; no Docker mutations."""
import importlib.util
import json
from pathlib import Path
import socket
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("ui_review", Path(__file__).with_name("ui-review.py"))
review = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(review)


class ReviewSafetyTests(unittest.TestCase):
    def test_container_requires_every_owned_boundary(self):
        owner = {"token": "owned-token", "container_id": "owned-id"}
        info = {"Name": "/bingo-ui-review", "Id": "owned-id", "Config": {"Labels": {review.LABEL: "owned-token"}},
                "HostConfig": {"PortBindings": {"5432/tcp": [{"HostIp": "127.0.0.1", "HostPort": "54339"}]}}}
        review.verify_container(info, owner)
        for name in ("bingowebpage-postgres-1", "bingo-admin-acceptance-20260928", "bingo-ticket-manual-20260914"):
            with self.subTest(name=name), self.assertRaises(RuntimeError):
                review.verify_container({**info, "Name": "/" + name}, owner)
        for changed in ({"Id": "foreign"}, {"Config": {"Labels": {review.LABEL: "foreign"}}},
                        {"HostConfig": {"PortBindings": {"5432/tcp": [{"HostIp": "0.0.0.0", "HostPort": "54339"}]}}}):
            with self.subTest(changed=changed), self.assertRaises(RuntimeError):
                review.verify_container({**info, **changed}, owner)

    def test_refresh_refuses_old_runtime_before_any_mutation(self):
        with tempfile.TemporaryDirectory(prefix="bingo-ur-version-") as directory:
            state = Path(directory)
            marker = state / "owner.json"
            marker.write_text(json.dumps({"token": "owned-token", "container_id": "owned-id", "marker_ready": True}))
            info = {"Name": "/bingo-ui-review", "Id": "owned-id", "Config": {"Image": "postgres:16-alpine", "Labels": {review.LABEL: "owned-token"}},
                    "HostConfig": {"PortBindings": {"5432/tcp": [{"HostIp": "127.0.0.1", "HostPort": "54339"}]}}, "State": {"Running": False}}
            with patch.object(review, "STATE", state), patch.object(review, "MARKER", marker), patch.object(review, "inspect", return_value=info), \
                    patch.object(review, "local_database_guard"), patch.object(review, "run") as run, patch.object(review, "stop_processes") as stop, \
                    patch.object(review.sys, "argv", ["ui-review.py", "refresh"]):
                self.assertEqual(review.IMAGE, "postgres:17-alpine")
                with self.assertRaisesRegex(RuntimeError, "image differs from postgres:17-alpine"):
                    review.main()
                run.assert_not_called()
                stop.assert_not_called()

    def test_database_marker_mismatch_is_refused(self):
        with patch.object(review, "sql", return_value=SimpleNamespace(stdout="foreign-owner\n")) as sql:
            with self.assertRaises(RuntimeError):
                review.verify_database({"token": "owned-token"})
            self.assertEqual(sql.call_count, 1)
            self.assertEqual(sql.call_args.args, ("SELECT token FROM public.bingo_ui_review_owner",))

    def test_appsettings_local_database_name_is_refused(self):
        with tempfile.TemporaryDirectory(prefix="bingo-ur-safety-") as directory:
            root = Path(directory)
            local = root / "src/Bingo.Web/appsettings.Local.json"
            local.parent.mkdir(parents=True)
            local.write_text(json.dumps({"ConnectionStrings": {"Database": "Host=localhost;Database=BINGO_UI_REVIEW;Password=synthetic"}}))
            with patch.object(review, "ROOT", root), self.assertRaises(RuntimeError):
                review.local_database_guard()

    def test_occupied_foreign_port_is_refused(self):
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            with self.assertRaisesRegex(RuntimeError, "No foreign process"):
                review.port_free(listener.getsockname()[1])

    def test_active_reusable_foreign_listener_is_refused(self):
        with socket.socket() as listener:
            listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            listener.bind(("127.0.0.1", 0))
            listener.listen()
            with self.assertRaisesRegex(RuntimeError, "No foreign process"):
                review.port_free(listener.getsockname()[1])

    def test_recently_closed_socket_does_not_block_refresh(self):
        with socket.socket() as listener:
            listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            listener.bind(("127.0.0.1", 0))
            port = listener.getsockname()[1]
            listener.listen()
            with socket.create_connection(("127.0.0.1", port)) as client:
                accepted, _ = listener.accept()
                with accepted:
                    accepted.shutdown(socket.SHUT_WR)
                    self.assertEqual(client.recv(1), b"")
                client.shutdown(socket.SHUT_WR)
        review.port_free(port)

    def test_changed_process_is_never_signalled(self):
        with tempfile.TemporaryDirectory(prefix="bingo-ur-safety-") as directory:
            records = Path(directory) / "processes.json"
            records.write_text(json.dumps([{"pid": 123, "identity": "Tue Oct 6 12:00:00 2026 dotnet owned.dll"}]))
            with patch.object(review, "PROCESSES", records), patch.object(review, "process_identity", return_value="Tue Oct 6 12:00:01 2026 dotnet foreign.dll"), patch.object(review.os, "kill") as kill:
                with self.assertRaises(RuntimeError):
                    review.stop_processes()
                kill.assert_not_called()

    def test_owned_argument_or_start_time_mismatch_is_never_signalled(self):
        with tempfile.TemporaryDirectory(prefix="bingo-ur-identity-") as directory:
            records = Path(directory) / "processes.json"
            command = ["/usr/bin/python3", "-m", "http.server", "5320", "--directory", "/owned/references"]
            records.write_text(json.dumps([{"pid": 123, "identity": "Tue Oct 6 12:00:00 2026 (python3.9)", "args": command}]))
            for changed in ("Tue Oct 6 12:00:00 2026 /usr/bin/python3 -m http.server 5320 --directory /foreign/references",
                            "Tue Oct 6 12:00:01 2026 /usr/bin/python3 -m http.server 5320 --directory /owned/references"):
                with self.subTest(changed=changed), patch.object(review, "PROCESSES", records), patch.object(review, "process_identity", return_value=changed), patch.object(review.os, "kill") as kill:
                    with self.assertRaises(RuntimeError):
                        review.stop_processes()
                    kill.assert_not_called()
                    original = records.read_text()
                    with self.assertRaises(RuntimeError):
                        review.capture_ready_processes(json.loads(original))
                    self.assertEqual(records.read_text(), original)
                    legacy = {"pid": 123, "identity": "Tue Oct 6 12:00:00 2026 (python3.9)"}
                    self.assertFalse(review.owns_process(legacy, changed))

    def test_command_line_tools_python_captures_ready_identity_and_stops_owned_child(self):
        with tempfile.TemporaryDirectory(prefix="bingo-ur-clt-") as directory:
            root = Path(directory)
            records = root / "processes.json"
            log = root / "reference.log"
            with socket.socket() as reservation:
                reservation.bind(("127.0.0.1", 0))
                port = reservation.getsockname()[1]
            args = ["/usr/bin/python3", "-m", "http.server", str(port), "--bind", "127.0.0.1", "--directory", str(root)]
            actual_identity = review.process_identity
            actual_popen = review.subprocess.Popen
            children = []
            def spawn(*values, **options):
                child = actual_popen(*values, **options)
                if values[0][0] == "/usr/bin/python3":
                    children.append(child)
                return child
            def identity(pid):
                # Retain/reap the fixture child; the real command detaches it when its parent exits.
                for child in children:
                    if child.pid == pid:
                        child.poll()
                return actual_identity(pid)
            def transient(pid):
                value = identity(pid)
                self.assertIsNotNone(value)
                return " ".join(value.split(maxsplit=5)[:5]) + " (python3.9)"
            with patch.object(review, "PROCESSES", records), patch.object(review.subprocess, "Popen", side_effect=spawn), patch.object(review, "process_identity", side_effect=identity):
                # Force the exact observed startup race while using the real CLT executable/child.
                with patch.object(review, "process_identity", side_effect=transient):
                    record = review.start_process(args, review.os.environ.copy(), log)
                records.write_text(json.dumps([record]))
                try:
                    review.ready(f"http://127.0.0.1:{port}/", log)
                    review.capture_ready_processes([record])
                    captured = json.loads(records.read_text())[0]
                    self.assertNotIn("(python3.9)", captured["identity"])
                    self.assertEqual(captured["args"], args)
                    self.assertTrue(review.owns_process(captured, actual_identity(captured["pid"])))
                    review.stop_processes()
                    self.assertFalse(records.exists())
                    self.assertIsNone(actual_identity(captured["pid"]))
                finally:
                    if records.exists():
                        review.stop_processes()

    def test_mac_python_reexec_retains_exact_arguments_and_start_time(self):
        shim = "Tue Oct 6 12:00:00 2026 /Library/Developer/CommandLineTools/usr/bin/python3 -m http.server 5320 --bind 127.0.0.1 --directory /owned/references"
        framework = shim.replace("/Library/Developer/CommandLineTools/usr/bin/python3", "/Library/Developer/CommandLineTools/Library/Frameworks/Python3.framework/Versions/3.9/Resources/Python.app/Contents/MacOS/Python")
        self.assertEqual(review.stable_identity(shim), review.stable_identity(framework))
        self.assertNotEqual(review.stable_identity(shim), review.stable_identity(framework.replace("/owned/", "/foreign/")))
        self.assertNotEqual(review.stable_identity(shim), review.stable_identity(framework.replace("12:00:00", "12:00:01")))

    def test_runtime_environment_cannot_inherit_live_wom_or_r2_settings(self):
        with patch.dict(review.os.environ, {"ASPNETCORE_ENVIRONMENT": "Production", "WiseOldMan__DevelopmentFake__Enabled": "false", "EvidenceStorage__Provider": "R2"}):
            env = review.environment()
            self.assertEqual(env["ASPNETCORE_ENVIRONMENT"], "Development")
            self.assertEqual(env["WiseOldMan__DevelopmentFake__Enabled"], "true")
            self.assertEqual(env["WiseOldMan__BaseUrl"], "http://127.0.0.1:1/")
            self.assertEqual(env["EvidenceStorage__Provider"], "Local")


if __name__ == "__main__":
    unittest.main()
