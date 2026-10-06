#!/usr/bin/env python3
"""Focused refusal tests; no Docker mutations or real process signals."""
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

    def test_changed_process_is_never_signalled(self):
        with tempfile.TemporaryDirectory(prefix="bingo-ur-safety-") as directory:
            records = Path(directory) / "processes.json"
            records.write_text(json.dumps([{"pid": 123, "identity": "Tue Oct 6 12:00:00 2026 dotnet owned.dll"}]))
            with patch.object(review, "PROCESSES", records), patch.object(review, "process_identity", return_value="Tue Oct 6 12:00:01 2026 dotnet foreign.dll"), patch.object(review.os, "kill") as kill:
                with self.assertRaises(RuntimeError):
                    review.stop_processes()
                kill.assert_not_called()

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
