#!/usr/bin/env python3
"""Owned, local Development UI review environment. No configurable DB target."""
import argparse
import json
import os
from pathlib import Path
import signal
import socket
import subprocess
import sys
import time
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
STATE = ROOT / "artifacts" / "ui-review"
NAME = "bingo-ui-review"
DB = "bingo_ui_review"
PORT = 54339
LABEL = "dev.bingo.ui-review.owner"
PASSWORD = "LocalReview!1234"
MARKER = STATE / "owner.json"
PROCESSES = STATE / "processes.json"


def run(*args, **kwargs):
    return subprocess.run(args, check=True, text=True, **kwargs)


def local_database_guard():
    local = ROOT / "src/Bingo.Web/appsettings.Local.json"
    if local.exists():
        settings = json.loads(local.read_text())
        for value in settings.get("ConnectionStrings", {}).values():
            for part in str(value).split(";"):
                key, _, val = part.partition("=")
                if key.strip().lower() in ("database", "initial catalog") and val.strip().lower() == DB:
                    raise RuntimeError("Refusing: appsettings.Local.json names the review database.")


def inspect():
    result = subprocess.run(["docker", "container", "inspect", NAME], text=True, capture_output=True)
    if result.returncode:
        # Distinguish absence from a Docker daemon failure before any write.
        run("docker", "info", stdout=subprocess.DEVNULL)
        return None
    return json.loads(result.stdout)[0]


def verify_container(info, owner):
    if info["Name"] != "/" + NAME or info["Config"].get("Labels", {}).get(LABEL) != owner["token"]:
        raise RuntimeError("Refusing: container name/owner label does not match the review marker.")
    binding = info["HostConfig"]["PortBindings"].get("5432/tcp", [])
    if binding != [{"HostIp": "127.0.0.1", "HostPort": str(PORT)}]:
        raise RuntimeError("Refusing: review PostgreSQL port binding differs from its fixed identity.")
    if info["Id"] != owner.get("container_id"):
        raise RuntimeError("Refusing: review container ID differs from the local marker.")


def sql(statement, database="postgres", capture=False):
    return run("docker", "exec", NAME, "psql", "-U", DB, "-d", database,
               "-v", "ON_ERROR_STOP=1", "-At", "-c", statement,
               capture_output=capture)


def verify_database(owner):
    result = sql("SELECT token FROM public.bingo_ui_review_owner", capture=True)
    if result.stdout.strip() != owner["token"]:
        raise RuntimeError("Refusing: PostgreSQL ownership marker does not match.")


def process_identity(pid):
    result = subprocess.run(["ps", "-p", str(pid), "-o", "lstart=", "-o", "command="],
                            text=True, capture_output=True)
    return result.stdout.strip() if result.returncode == 0 else None


def stop_processes():
    if not PROCESSES.exists():
        return
    records = json.loads(PROCESSES.read_text())
    for record in records:
        identity = process_identity(record["pid"])
        if identity and identity != record["identity"]:
            raise RuntimeError("Refusing to stop a process whose identity no longer matches the owned PID.")
    for record in records:
        if process_identity(record["pid"]):
            os.kill(record["pid"], signal.SIGTERM)
    for _ in range(100):
        if all(process_identity(record["pid"]) is None for record in records):
            PROCESSES.unlink()
            return
        time.sleep(.1)
    raise RuntimeError("Owned processes did not stop; no forced kill was attempted.")


def port_free(port):
    with socket.socket() as probe:
        try:
            probe.bind(("127.0.0.1", port))
        except OSError as error:
            raise RuntimeError(f"Port {port} is occupied; stop its owner yourself. No foreign process will be killed.") from error


def environment():
    env = os.environ.copy()
    env.update({
        "ASPNETCORE_ENVIRONMENT": "Development", "DOTNET_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": "http://127.0.0.1:5310",
        "ASPNETCORE_CONTENTROOT": str(ROOT / "src/Bingo.Web"),
        "ConnectionStrings__Database": f"Host=127.0.0.1;Port={PORT};Database={DB};Username={DB};Password={PASSWORD}",
        "UiReviewEnvironment__Enabled": "true",
        "EvidenceStorage__Provider": "Local", "EvidenceStorage__LocalPath": str(STATE / "evidence"),
        "CatalogueImageCache__LocalPath": str(STATE / "catalogue-images"),
        "WiseOldMan__BaseUrl": "http://127.0.0.1:1/",
        "WiseOldMan__ApiKey": "", "WiseOldMan__DevelopmentFake__Enabled": "true",
        "WiseOldMan__DevelopmentFake__AutomaticSynchronizationEnabled": "false",
        "DevelopmentAdminBootstrap__Enabled": "false",
    })
    return env


def dotnet(env, *args):
    run("dotnet", "run", "--project", str(ROOT / "src/Bingo.Web"),
        "--configuration", "Release", "--no-build", "--no-launch-profile", "--", *args,
        cwd=ROOT, env=env)


def start_process(args, env, log):
    with log.open("w") as output:
        process = subprocess.Popen(args, cwd=ROOT, env=env, stdout=output, stderr=subprocess.STDOUT,
                                   start_new_session=True)
    identity = process_identity(process.pid)
    if not identity:
        raise RuntimeError(f"Process exited at startup; see {log}")
    return {"pid": process.pid, "identity": identity}


def ready(url, log):
    for _ in range(150):
        try:
            with urllib.request.urlopen(url, timeout=1) as response:
                if response.status == 200:
                    return
        except (OSError, urllib.error.URLError):
            time.sleep(.2)
    raise RuntimeError(f"Server did not become ready: {url}; see {log}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["create", "refresh", "stop"], nargs="?", default="create")
    parser.add_argument("profile", choices=["live", "final-review"], nargs="?", default="live")
    args = parser.parse_args()
    local_database_guard()
    info = inspect()
    owner = json.loads(MARKER.read_text()) if MARKER.exists() else None
    if info:
        if not owner:
            raise RuntimeError("Refusing: container exists without this checkout's ownership marker.")
        verify_container(info, owner)
        if info["State"]["Running"]:
            verify_database(owner)
    elif owner:
        raise RuntimeError("Refusing: ownership marker exists but its container is missing.")
    if args.action == "stop":
        stop_processes()
        if info and info["State"]["Running"]:
            run("docker", "stop", NAME)
        print("Owned UI review app, reference server and PostgreSQL stopped.")
        return
    stop_processes()
    port_free(5310)
    port_free(5320)
    STATE.mkdir(parents=True, exist_ok=True)
    if info is None:
        port_free(PORT)
        owner = {"token": str(uuid.uuid4())}
        run("docker", "run", "-d", "--name", NAME, "--label", LABEL + "=" + owner["token"],
            "-p", f"127.0.0.1:{PORT}:5432", "-e", "POSTGRES_USER=" + DB,
            "-e", "POSTGRES_PASSWORD=" + PASSWORD, "-e", "POSTGRES_DB=" + DB, "postgres:16-alpine")
        owner["container_id"] = inspect()["Id"]
        MARKER.write_text(json.dumps(owner))
        new = True
    else:
        new = False
        if not info["State"]["Running"]:
            port_free(PORT)
            run("docker", "start", NAME)
    for _ in range(100):
        result = subprocess.run(["docker", "exec", NAME, "pg_isready", "-U", DB], capture_output=True)
        if result.returncode == 0:
            break
        time.sleep(.2)
    else:
        raise RuntimeError("Owned PostgreSQL did not become ready.")
    verify_container(inspect(), owner)
    if new:
        sql("CREATE TABLE public.bingo_ui_review_owner (token text NOT NULL); INSERT INTO public.bingo_ui_review_owner VALUES ('" + owner["token"] + "')")
    verify_database(owner)
    # Only this fixed database on the verified owned container can be rebuilt.
    sql(f"DROP DATABASE IF EXISTS {DB} WITH (FORCE)")
    sql(f"CREATE DATABASE {DB}")
    env = environment()
    run("dotnet", "build", str(ROOT / "Bingo.slnx"), "--configuration", "Release", cwd=ROOT, env=env)
    dotnet(env, "--migrate")
    dotnet(env, "--apply-catalogue-snapshot")
    dotnet(env, "--seed-review-scenarios", "--review-profile", args.profile)
    records = []
    try:
        records.append(start_process(["dotnet", str(ROOT / "src/Bingo.Web/bin/Release/net10.0/Bingo.Web.dll")], env, STATE / "app.log"))
        PROCESSES.write_text(json.dumps(records))
        records.append(start_process([sys.executable, "-m", "http.server", "5320", "--bind", "127.0.0.1", "--directory", str(ROOT / "docs/references/admin-ui")], env, STATE / "references.log"))
        PROCESSES.write_text(json.dumps(records))
        ready("http://127.0.0.1:5310/Account/Login", STATE / "app.log")
        ready("http://127.0.0.1:5320/", STATE / "references.log")
    except Exception:
        stop_processes()
        raise
    print("App: http://127.0.0.1:5310 | References: http://127.0.0.1:5320")
    catalogue = STATE / "scenarios.md"
    if catalogue.exists():
        print(catalogue.read_text())
    print(f"Scenario list: {catalogue}")


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, subprocess.CalledProcessError) as error:
        print(f"UI review stopped: {error}", file=sys.stderr)
        sys.exit(1)
