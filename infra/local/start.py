#!/usr/bin/env python
# /// script
# requires-python = ">=3.10"
# dependencies = ["InquirerPy==0.3.4"]
# ///

from __future__ import annotations

import argparse
import atexit
import json
import os
import secrets
import shutil
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any, Sequence


class LauncherError(Exception):
    pass


HOSTING_MODES = ("Combined", "Split")
HOSTING_MODE_LABELS = {
    "Combined": "Combined (API and background processing in one container)",
    "Split": "Split (separate API and Worker containers)",
}
PROVIDERS = ("SqlServer", "PostgreSQL", "SQLite", "MySQL")
PROVIDER_LABELS = {
    "SqlServer": "SQL Server",
    "PostgreSQL": "PostgreSQL",
    "SQLite": "SQLite",
    "MySQL": "MySQL",
}
AUTH0_KEYS = (
    "Domain",
    "ClientId",
    "ClientSecret",
    "Audience",
    "ManagementClientId",
    "ManagementClientSecret",
)
ALL_COMPOSE_PROFILES = ("SqlServer", "PostgreSQL", "SQLite", "MySQL")
WATCH_PROJECTS = (
    "RefTestManagement.Api",
    "RefTestManagement.Application",
    "RefTestManagement.Domain",
    "RefTestManagement.Hosting",
    "RefTestManagement.Hosting.Azure",
    "RefTestManagement.Infrastructure",
    "RefTestManagement.Migrations.MySQL",
    "RefTestManagement.Migrations.PostgreSQL",
    "RefTestManagement.Migrations.SQLite",
    "RefTestManagement.Migrations.SqlServer",
    "RefTestManagement.Worker",
    "RefTestManagement.Ui",
)
IGNORED_WATCH_DIRECTORIES = {
    ".angular",
    ".git",
    "bin",
    "coverage",
    "dist",
    "node_modules",
    "obj",
    "TestResults",
    "wwwroot",
}
ROOT = Path(__file__).resolve().parents[2]


def is_valid_hosting_mode(value: object) -> bool:
    return value in HOSTING_MODES


def is_valid_database_provider(value: object) -> bool:
    return isinstance(value, str) and value in PROVIDERS


def source_snapshot(root: Path) -> dict[str, tuple[int, int]]:
    snapshot: dict[str, tuple[int, int]] = {}
    for project in WATCH_PROJECTS:
        project_root = root / project
        if not project_root.is_dir():
            continue
        for directory, subdirectories, filenames in os.walk(project_root):
            subdirectories[:] = [
                name for name in subdirectories if name not in IGNORED_WATCH_DIRECTORIES
            ]
            for filename in filenames:
                path = Path(directory) / filename
                try:
                    metadata = path.stat()
                except FileNotFoundError:
                    continue
                snapshot[str(path.relative_to(root))] = (
                    metadata.st_mtime_ns,
                    metadata.st_size,
                )
    return snapshot


def _prompt_style() -> Any:
    from InquirerPy.utils import get_style

    return get_style(
        {
            "questionmark": "#e5c07b bold",
            "question": "bold",
            "answer": "#98c379 bold",
            "instruction": "#abb2bf",
            "pointer": "#61afef bold",
            "checkbox": "#98c379 bold",
            "validator": "#e06c75 bold",
        },
        style_override=False,
    )


def select_hosting_mode() -> str:
    from InquirerPy import inquirer
    from InquirerPy.base.control import Choice

    choices = [
        Choice(value=mode, name=HOSTING_MODE_LABELS[mode])
        for mode in HOSTING_MODES
    ]
    try:
        selected = inquirer.select(
            message="Choose the local hosting mode:",
            choices=choices,
            qmark="?",
            amark="✓",
            pointer="❯",
            style=_prompt_style(),
        ).execute()
    except (EOFError, KeyboardInterrupt) as error:
        raise LauncherError("Hosting mode selection was cancelled.") from error

    if not is_valid_hosting_mode(selected):
        raise LauncherError("Select a supported local hosting mode.")
    return selected


def select_database_provider() -> str:
    from InquirerPy import inquirer
    from InquirerPy.base.control import Choice

    choices = [
        Choice(value=provider, name=PROVIDER_LABELS[provider], enabled=False)
        for provider in PROVIDERS
    ]

    try:
        selected = inquirer.select(
            message="Choose the database provider:",
            choices=choices,
            qmark="?",
            amark="✓",
            pointer="❯",
            style=_prompt_style(),
        ).execute()
    except (EOFError, KeyboardInterrupt) as error:
        raise LauncherError("Database provider selection was cancelled.") from error

    if not is_valid_database_provider(selected):
        raise LauncherError("Select a supported database provider.")
    return selected


class LocalStackLauncher:
    def __init__(self) -> None:
        self.root = ROOT
        self.machine_name = "rtm-local"
        self.project_name = "rtm-local"
        self.network_name = f"{self.project_name}-nodns"
        self.network_subnet = "10.250.241.0/24"
        self.compose_file = self.root / "compose.local.yaml"
        self.local_directory = self.root / "infra" / "local"
        self.env_file = self.local_directory / ".env"
        self.certificate_directory = self.local_directory / "certs"
        self.image_directory = self.root / "artifacts" / "local"
        self.api_project = self.root / "RefTestManagement.Api" / "RefTestManagement.Api.csproj"
        self.api_image = "localhost/ref-test-management-api:local"
        self.worker_image = "localhost/ref-test-management-worker:local"
        self.api_archive = self.image_directory / "ref-test-management-api.tar.gz"
        self.worker_archive = self.image_directory / "ref-test-management-worker.tar.gz"
        self.user_secrets_id = self._read_user_secrets_id()
        self.user_secrets_file = (
            self.local_directory
            / "user-secrets"
            / self.user_secrets_id
            / "secrets.json"
        )
        self.hosting_mode: str | None = None
        self.database_provider: str | None = None
        self.database_password: str | None = None
        self.database_connection_string: str | None = None
        self.database_service: str | None = None
        self.database_ready_command: str | None = None
        self.database_ready_description: str | None = None
        self.connection_name: str | None = None
        self.compose_env: dict[str, str] | None = None
        self.ssh_process: subprocess.Popen[Any] | None = None
        self.local_env: dict[str, str] = {}

    def _read_user_secrets_id(self) -> str:
        try:
            root = ET.parse(self.api_project).getroot()
        except (ET.ParseError, OSError) as error:
            raise LauncherError("Could not read the API project's UserSecretsId.") from error

        identifiers = [
            (node.text or "").strip()
            for node in root.iter()
            if node.tag.rsplit("}", 1)[-1] == "UserSecretsId"
        ]
        identifiers = [identifier for identifier in identifiers if identifier]
        if len(identifiers) != 1:
            raise LauncherError("Could not find exactly one UserSecretsId in the API project.")
        return identifiers[0]

    @staticmethod
    def _display_name(args: Sequence[str | Path] | str) -> str:
        if isinstance(args, str):
            return args.split(maxsplit=1)[0] if args else "command"
        return Path(str(args[0])).name if args else "command"

    def _run(
        self,
        args: Sequence[str | Path] | str,
        *,
        cwd: Path | None = None,
        env: dict[str, str] | None = None,
        capture: bool = False,
        quiet: bool = False,
        shell: bool = False,
        check: bool = True,
        error_message: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        command: Sequence[str] | str
        if isinstance(args, str):
            command = args
        else:
            command = [str(argument) for argument in args]

        output_target: int | None = subprocess.PIPE if capture else None
        error_target: int | None = subprocess.PIPE if capture else None
        if quiet and not capture:
            output_target = subprocess.DEVNULL
            error_target = subprocess.DEVNULL

        try:
            result = subprocess.run(
                command,
                cwd=str(cwd or self.root),
                env=env,
                text=True,
                stdout=output_target,
                stderr=error_target,
                shell=shell,
                check=False,
            )
        except OSError as error:
            raise LauncherError(
                error_message or f"Could not start required command '{self._display_name(args)}'."
            ) from error

        if check and result.returncode != 0:
            raise LauncherError(
                error_message
                or f"Command '{self._display_name(args)}' failed with exit code {result.returncode}."
            )
        return result

    def _output(
        self,
        args: Sequence[str | Path],
        *,
        env: dict[str, str] | None = None,
        error_message: str,
    ) -> str:
        result = self._run(args, env=env, capture=True, error_message=error_message)
        return result.stdout or ""

    def _json_output(
        self,
        args: Sequence[str | Path],
        *,
        error_message: str,
    ) -> Any:
        output = self._output(args, error_message=error_message)
        try:
            return json.loads(output)
        except json.JSONDecodeError as error:
            raise LauncherError(error_message) from error

    def require_tools(self) -> None:
        if os.name != "nt":
            raise LauncherError("This local Podman launcher requires a Windows host.")
        required = ["podman", "dotnet", "npm", "node", "openssl", "curl"]
        if os.name == "nt":
            required.extend(("icacls", "whoami"))
        missing = [name for name in required if shutil.which(name) is None]
        if missing:
            raise LauncherError(f"Required tool(s) not found: {', '.join(missing)}.")

    def podman(
        self,
        *args: str | Path,
        connection: str | None = None,
        env: dict[str, str] | None = None,
        capture: bool = False,
        quiet: bool = False,
        check: bool = True,
        error_message: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        command: list[str | Path] = ["podman"]
        if connection:
            command.extend(("--connection", connection))
        command.extend(args)
        return self._run(
            command,
            env=env,
            capture=capture,
            quiet=quiet,
            check=check,
            error_message=error_message,
        )

    def compose(
        self,
        *args: str | Path,
        profiles: Sequence[str] = (),
        env: dict[str, str] | None = None,
        capture: bool = False,
        quiet: bool = False,
        error_message: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        if not self.connection_name:
            raise LauncherError("The isolated Podman connection was not initialized.")
        if env is None:
            env = self.compose_env

        command: list[str | Path] = [
            "podman",
            "--connection",
            self.connection_name,
            "compose",
            "--project-name",
            self.project_name,
            "--file",
            self.compose_file,
            "--env-file",
            self.env_file,
        ]
        for profile in profiles:
            command.extend(("--profile", profile))
        command.extend(args)
        return self._run(
            command,
            env=env,
            capture=capture,
            quiet=quiet,
            error_message=error_message,
        )

    def _ensure_machine(self) -> None:
        machines = self._json_output(
            ("podman", "machine", "list", "--format", "json"),
            error_message="Could not list Podman machines.",
        )
        if not isinstance(machines, list):
            raise LauncherError("Podman returned an invalid machine list.")
        machine = next(
            (item for item in machines if item.get("Name") == self.machine_name),
            None,
        )
        if machine is None:
            self.podman(
                "machine",
                "init",
                "--cpus",
                "4",
                "--memory",
                "6144",
                "--disk-size",
                "60",
                self.machine_name,
            )
            machines = self._json_output(
                ("podman", "machine", "list", "--format", "json"),
                error_message="Could not verify the isolated Podman machine.",
            )
            machine = next(
                (item for item in machines if item.get("Name") == self.machine_name),
                None,
            )
        if machine is None:
            raise LauncherError(f"Podman did not register the isolated machine '{self.machine_name}'.")

        try:
            memory = int(machine.get("Memory") or 0)
        except (TypeError, ValueError) as error:
            raise LauncherError(f"Could not read the memory assigned to '{self.machine_name}'.") from error
        if memory < 6 * 1024 * 1024 * 1024:
            raise LauncherError(
                f"The existing '{self.machine_name}' machine has less than 6 GiB RAM; it was not resized."
            )

        user_mode_networking = machine.get("UserModeNetworking") is True
        machine_running = machine.get("Running") is True
        if not user_mode_networking:
            if machine_running:
                raise LauncherError(
                    "The isolated machine needs user-mode networking but is running. "
                    f"Stop only '{self.machine_name}' manually, then rerun."
                )
            self.podman("machine", "set", "--user-mode-networking", self.machine_name)
            machines = self._json_output(
                ("podman", "machine", "list", "--format", "json"),
                error_message=f"Could not verify user-mode networking on '{self.machine_name}'.",
            )
            machine = next(
                (item for item in machines if item.get("Name") == self.machine_name),
                None,
            )
            if machine is None or machine.get("UserModeNetworking") is not True:
                raise LauncherError(f"Podman did not enable user-mode networking on '{self.machine_name}'.")

        if not machine_running:
            self.podman("machine", "start", self.machine_name)

    def _find_rootless_connection(self) -> str:
        connections = self._json_output(
            ("podman", "system", "connection", "list", "--format", "json"),
            error_message="Could not find the isolated rootless Podman connection.",
        )
        if not isinstance(connections, list):
            raise LauncherError("Podman returned an invalid system connection list.")
        connection = next(
            (
                item
                for item in connections
                if item.get("Name") == self.machine_name
                and "/run/user/" in str(item.get("URI") or "")
            ),
            None,
        )
        if connection is None or not connection.get("Name"):
            raise LauncherError(f"Could not find the rootless Podman connection for '{self.machine_name}'.")
        return str(connection["Name"])

    def _get_default_connection(self) -> str:
        connections = self._json_output(
            ("podman", "system", "connection", "list", "--format", "json"),
            error_message="Could not read Podman system connections.",
        )
        if not isinstance(connections, list):
            raise LauncherError("Podman returned an invalid system connection list.")
        for connection in connections:
            if connection.get("Default") is True:
                return str(connection.get("Name") or "")
        return ""

    def _ensure_rootless_podman_api(self, connection_name: str) -> None:
        info_args = (
            "--connection",
            connection_name,
            "info",
            "--format",
            "{{.Host.Hostname}}",
        )
        if self.podman(*info_args, capture=True, check=False, quiet=True).returncode == 0:
            return

        remote_command = (
            "sh -c 'mkdir -p /run/user/1000/podman && chmod 700 /run/user/1000/podman "
            "&& nohup env XDG_RUNTIME_DIR=/run/user/1000 podman system service --time=0 "
            "unix:///run/user/1000/podman/podman.sock >/tmp/rtm-podman-api.log "
            "2>&1 </dev/null &'"
        )
        try:
            self.ssh_process = subprocess.Popen(
                ["podman", "machine", "ssh", self.machine_name, remote_command],
                cwd=str(self.root),
                stdin=subprocess.DEVNULL,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            )
        except OSError as error:
            raise LauncherError(f"Could not start the Podman API socket in '{self.machine_name}'.") from error

        for _ in range(30):
            if self.podman(*info_args, capture=True, check=False, quiet=True).returncode == 0:
                if self.ssh_process.poll() is not None:
                    self.ssh_process = None
                return
            time.sleep(1)
        raise LauncherError(f"Could not start the rootless Podman API socket in '{self.machine_name}'.")

    def _ensure_local_network(self, connection_name: str) -> None:
        networks = self._json_output(
            ("podman", "--connection", connection_name, "network", "ls", "--format", "json"),
            error_message="Could not list isolated Podman networks.",
        )
        if not isinstance(networks, list):
            raise LauncherError("Podman returned an invalid network list.")
        if not any(network.get("name") == self.network_name for network in networks):
            self.podman(
                "--connection",
                connection_name,
                "network",
                "create",
                "--disable-dns",
                "--subnet",
                self.network_subnet,
                self.network_name,
                quiet=True,
            )

        details = self._json_output(
            ("podman", "--connection", connection_name, "network", "inspect", self.network_name),
            error_message="Could not inspect the isolated Podman network.",
        )
        try:
            network = details[0]
            dns_enabled = network["dns_enabled"]
            subnet = network["subnets"][0]["subnet"]
        except (IndexError, KeyError, TypeError) as error:
            raise LauncherError("The isolated Podman network has an invalid configuration.") from error
        if dns_enabled is not False or subnet != self.network_subnet:
            raise LauncherError(
                f"The isolated Podman network '{self.network_name}' has an unexpected DNS or subnet configuration; it was not changed."
            )

    @staticmethod
    def _read_env_file(path: Path) -> dict[str, str]:
        values: dict[str, str] = {}
        try:
            for line in path.read_text(encoding="utf-8").splitlines():
                if not line or line.lstrip().startswith("#") or "=" not in line:
                    continue
                key, value = line.split("=", 1)
                values.setdefault(key, value)
        except OSError as error:
            raise LauncherError("Could not read the ignored local environment file.") from error
        return values

    def _restrict_file_permissions(self, path: Path) -> None:
        if os.name != "nt":
            try:
                os.chmod(path, 0o600)
            except OSError as error:
                raise LauncherError("Could not restrict permissions on a local secret file.") from error
            return

        identity = self._output(("whoami",), error_message="Could not determine the current Windows account.").strip()
        if not identity:
            raise LauncherError("Could not determine the current Windows account.")
        self._run(
            ("icacls", path, "/inheritance:r"),
            capture=True,
            error_message="Could not restrict permissions on a local secret file.",
        )
        self._run(
            (
                "icacls",
                path,
                "/grant:r",
                f"{identity}:(F)",
                "*S-1-5-18:(F)",
                "*S-1-5-32-544:(F)",
            ),
            capture=True,
            error_message="Could not restrict permissions on a local secret file.",
        )

    def _write_local_environment(self) -> None:
        self.local_directory.mkdir(parents=True, exist_ok=True)
        if self.env_file.exists():
            self.local_env = self._read_env_file(self.env_file)
            for name in ("REDIS_PASSWORD", "PRIVACY_CHALLENGE_HMAC_SECRET"):
                if not self.local_env.get(name):
                    raise LauncherError(
                        f"The ignored local environment file is missing {name}. Repair it locally; values are never printed."
                    )
            self.database_password = self.local_env.get("DATABASE_PASSWORD") or self.local_env.get(
                "MSSQL_SA_PASSWORD"
            )
            if not self.database_password:
                raise LauncherError(
                    "The ignored local environment file is missing DATABASE_PASSWORD (or legacy MSSQL_SA_PASSWORD). Repair it locally; values are never printed."
                )
            return

        self.database_password = f"LocalA1!{secrets.token_hex(24)}"
        redis_password = f"R{secrets.token_hex(32)}"
        hmac_secret = f"H{secrets.token_hex(32)}"
        values = {
            "DATABASE_PASSWORD": self.database_password,
            "REDIS_PASSWORD": redis_password,
            "PRIVACY_CHALLENGE_HMAC_SECRET": hmac_secret,
        }
        content = "".join(f"{key}={value}\n" for key, value in values.items())
        try:
            descriptor = os.open(
                self.env_file,
                os.O_CREAT | os.O_EXCL | os.O_WRONLY,
                0o600,
            )
        except FileExistsError:
            self.local_env = self._read_env_file(self.env_file)
            self.database_password = self.local_env.get("DATABASE_PASSWORD") or self.local_env.get(
                "MSSQL_SA_PASSWORD"
            )
            if not self.database_password:
                raise LauncherError(
                    "The ignored local environment file is missing DATABASE_PASSWORD (or legacy MSSQL_SA_PASSWORD). Repair it locally; values are never printed."
                )
            for name in ("REDIS_PASSWORD", "PRIVACY_CHALLENGE_HMAC_SECRET"):
                if not self.local_env.get(name):
                    raise LauncherError(
                        f"The ignored local environment file is missing {name}. Repair it locally; values are never printed."
                    )
            return
        try:
            with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as env_stream:
                env_stream.write(content)
            self._restrict_file_permissions(self.env_file)
        except (OSError, LauncherError):
            self.env_file.unlink(missing_ok=True)
            raise
        self.local_env = values

    def _copy_api_user_secrets(self) -> None:
        app_data = os.environ.get("APPDATA")
        if not app_data:
            raise LauncherError("Could not locate the API project's User Secrets directory; values were not displayed.")
        source = Path(app_data) / "Microsoft" / "UserSecrets" / self.user_secrets_id / "secrets.json"
        self.user_secrets_file.parent.mkdir(parents=True, exist_ok=True)
        if source.is_file():
            self._run(
                ("dotnet", "user-secrets", "list", "--project", self.api_project),
                capture=True,
                error_message="Could not validate the API project's User Secrets file; values were not displayed.",
            )
            try:
                shutil.copyfile(source, self.user_secrets_file)
            except OSError as error:
                raise LauncherError("Could not copy the API project's User Secrets file; values were not displayed.") from error
        else:
            self.user_secrets_file.write_text("{}\n", encoding="utf-8")
        self._restrict_file_permissions(self.user_secrets_file)

    def _ensure_required_auth0_settings(self) -> None:
        output = self._output(
            ("dotnet", "user-secrets", "list", "--project", self.api_project),
            error_message="Could not read the API project's User Secrets; required Auth0 settings were not displayed.",
        )
        user_secrets: dict[str, str] = {}
        for line in output.splitlines():
            key, separator, value = line.partition(" = ")
            if separator:
                user_secrets[key] = value

        missing: list[str] = []
        for key in AUTH0_KEYS:
            environment_name = f"Identity__Auth0__{key}"
            if environment_name in os.environ:
                value = os.environ[environment_name]
            elif environment_name in self.local_env:
                value = self.local_env[environment_name]
            else:
                value = user_secrets.get(f"Identity:Auth0:{key}", "")
            if not value.strip():
                missing.append(f"Identity:Auth0:{key}")
        if missing:
            names = ", ".join(missing)
            raise LauncherError(
                f"Required Auth0 settings are missing or empty: {names}. Set them in the API project's User Secrets or non-empty Identity__Auth0__<Key> environment overrides; values were not displayed."
            )

    def _ensure_local_https_certificate(self) -> None:
        certificate = self.certificate_directory / "localhost.crt"
        private_key = self.certificate_directory / "localhost.key"
        check_result = self._run(
            ("dotnet", "dev-certs", "https", "--check", "--trust"),
            quiet=True,
            check=False,
        )
        if check_result.returncode != 0:
            raise LauncherError(
                "A trusted .NET HTTPS development certificate is required. Run 'dotnet dev-certs https --trust' once, then rerun."
            )
        self._run(
            (
                "dotnet",
                "dev-certs",
                "https",
                "--export-path",
                certificate,
                "--format",
                "PEM",
                "--no-password",
            ),
            quiet=True,
            error_message="Could not export the trusted .NET HTTPS development certificate to the ignored local certificate directory.",
        )
        if not certificate.is_file() or not private_key.is_file():
            raise LauncherError(
                "The .NET HTTPS development certificate export did not create the expected local certificate files."
            )
        self._restrict_file_permissions(private_key)

    def _ensure_redis_certificates(self) -> None:
        ca_certificate = self.certificate_directory / "redis-ca.crt"
        server_certificate = self.certificate_directory / "redis.crt"
        server_key = self.certificate_directory / "redis.key"
        ca_bundle = self.certificate_directory / "redis-ca-bundle.crt"
        existing_count = sum(path.is_file() for path in (ca_certificate, server_certificate, server_key))
        if 0 < existing_count < 3:
            raise LauncherError(
                "The local Redis certificate set is incomplete. Preserve it for inspection; do not delete it automatically."
            )

        if existing_count == 0:
            with tempfile.TemporaryDirectory(prefix="rtm-local-certs-") as temporary:
                temporary_path = Path(temporary)
                self._run(
                    (
                        "openssl",
                        "req",
                        "-x509",
                        "-newkey",
                        "rsa:3072",
                        "-sha256",
                        "-days",
                        "365",
                        "-nodes",
                        "-keyout",
                        temporary_path / "ca.key",
                        "-out",
                        temporary_path / "ca.crt",
                        "-subj",
                        "/CN=RefTestManagement Local Redis CA",
                        "-addext",
                        "basicConstraints=critical,CA:TRUE",
                        "-addext",
                        "keyUsage=critical,keyCertSign,cRLSign",
                    ),
                    quiet=True,
                    error_message="Could not create the local Redis certificate authority.",
                )
                self._run(
                    (
                        "openssl",
                        "req",
                        "-newkey",
                        "rsa:2048",
                        "-nodes",
                        "-keyout",
                        temporary_path / "redis.key",
                        "-out",
                        temporary_path / "redis.csr",
                        "-subj",
                        "/CN=redis",
                        "-addext",
                        "subjectAltName=DNS:redis",
                    ),
                    quiet=True,
                    error_message="Could not create the local Redis server certificate request.",
                )
                self._run(
                    (
                        "openssl",
                        "x509",
                        "-req",
                        "-in",
                        temporary_path / "redis.csr",
                        "-CA",
                        temporary_path / "ca.crt",
                        "-CAkey",
                        temporary_path / "ca.key",
                        "-CAcreateserial",
                        "-out",
                        server_certificate,
                        "-days",
                        "365",
                        "-sha256",
                        "-copy_extensions",
                        "copy",
                    ),
                    quiet=True,
                    error_message="Could not sign the local Redis server certificate.",
                )
                shutil.copyfile(temporary_path / "ca.crt", ca_certificate)
                shutil.copyfile(temporary_path / "redis.key", server_key)

        self._run(
            (
                "openssl",
                "verify",
                "-CAfile",
                ca_certificate,
                "-verify_hostname",
                "redis",
                server_certificate,
            ),
            quiet=True,
            error_message="The local Redis certificate failed its hostname/CA check.",
        )
        self._restrict_file_permissions(server_key)

        if not ca_bundle.is_file():
            if not self.connection_name:
                raise LauncherError("The isolated Podman connection was not initialized.")
            result = self._run(
                (
                    "podman",
                    "--connection",
                    self.connection_name,
                    "run",
                    "--rm",
                    "--pull=missing",
                    "--entrypoint",
                    "cat",
                    self.api_image,
                    "/etc/ssl/certs/ca-certificates.crt",
                ),
                capture=True,
                error_message="Could not read the .NET container's system CA bundle.",
            )
            base_bundle = result.stdout or ""
            if not base_bundle.strip():
                raise LauncherError("The .NET container CA bundle was empty.")
            ca_bundle.write_text(
                f"{base_bundle.rstrip()}\n{ca_certificate.read_text(encoding='ascii')}",
                encoding="ascii",
                newline="\n",
            )

    def _wait_container_command(
        self,
        container_name: str,
        command: str,
        description: str,
        attempts: int = 90,
        interval: float = 2,
    ) -> None:
        if not self.connection_name:
            raise LauncherError("The isolated Podman connection was not initialized.")
        for _ in range(attempts):
            result = self.podman(
                "--connection",
                self.connection_name,
                "exec",
                container_name,
                "sh",
                "-c",
                command,
                capture=True,
                check=False,
            )
            if result.returncode == 0:
                return
            time.sleep(interval)
        raise LauncherError(
            f"{description} did not become ready in time; inspect only the isolated '{self.project_name}' project."
        )

    def _container_state(self, container_name: str) -> tuple[str, int, int]:
        if not self.connection_name:
            raise LauncherError("The isolated Podman connection was not initialized.")
        output = self._output(
            (
                "podman",
                "--connection",
                self.connection_name,
                "container",
                "inspect",
                container_name,
                "--format",
                "{{.State.Status}}|{{.State.ExitCode}}|{{.RestartCount}}",
            ),
            error_message=f"Could not inspect container '{container_name}'.",
        ).strip()
        try:
            status, exit_code, restart_count = output.split("|")
            return status, int(exit_code), int(restart_count)
        except ValueError as error:
            raise LauncherError(f"Container '{container_name}' returned invalid state.") from error

    def _compose_container_id(self, service: str, include_stopped: bool = False) -> str:
        profiles = (self.database_provider,) if self.database_provider else ()
        # One-shot services (migrate) have already exited by the time they are looked up.
        ps_args = ("ps", "--all", "--quiet", service) if include_stopped else ("ps", "--quiet", service)
        output = self.compose(*ps_args, profiles=profiles, capture=True).stdout or ""
        identifiers = [line.strip() for line in output.splitlines() if line.strip()]
        if len(identifiers) != 1:
            raise LauncherError(f"Expected exactly one container for Compose service '{service}'.")
        return identifiers[0]

    def _remove_migration_container(self, env: dict[str, str]) -> None:
        # Called only after the application services are running: api and worker
        # depend on migrate completing, so removing it earlier would break a later `up`.
        profiles = (self.database_provider,) if self.database_provider else ()
        try:
            self.compose("rm", "--force", "migrate", profiles=profiles, env=env, quiet=True)
        except LauncherError as error:
            print(f"Warning: could not remove the completed migration container: {error}")

    def _wait_container_completion(
        self,
        container_name: str,
        description: str,
        attempts: int = 120,
        interval: float = 2,
    ) -> None:
        for _ in range(attempts):
            status, exit_code, _ = self._container_state(container_name)
            if status == "exited":
                if exit_code != 0:
                    raise LauncherError(f"{description} failed with exit code {exit_code}.")
                return
            if status == "dead":
                raise LauncherError(f"{description} stopped in the dead state.")
            time.sleep(interval)
        raise LauncherError(f"{description} did not complete in time.")

    def _wait_container_running(
        self,
        container_name: str,
        description: str,
        attempts: int = 60,
        interval: float = 2,
    ) -> None:
        for _ in range(attempts):
            status, _, restart_count = self._container_state(container_name)
            if restart_count != 0:
                raise LauncherError(f"{description} restarted {restart_count} times during startup.")
            if status == "running":
                return
            time.sleep(interval)
        raise LauncherError(f"{description} did not remain running in time.")

    def _database_connection_string(self) -> str:
        if not self.database_password:
            raise LauncherError("The local database password is unavailable.")
        password = self.database_password
        if self.database_provider == "SqlServer":
            return (
                "Server=database,1433;Database=RefTestManagement;User ID=sa;"
                f"Password={password};Encrypt=True;TrustServerCertificate=True;"
            )
        if self.database_provider == "PostgreSQL":
            return f"Host=database;Database=RefTestManagement;Username=postgres;Password={password}"
        if self.database_provider == "SQLite":
            return "Data Source=/var/lib/ref-test-management/RefTestManagement.db"
        if self.database_provider == "MySQL":
            return f"Server=database;Database=RefTestManagement;User=root;Password={password};"
        raise LauncherError("Unsupported database provider selection.")

    def _configure_database(self) -> dict[str, str]:
        if (
            not self.database_provider
            or not self.database_password
            or not is_valid_hosting_mode(self.hosting_mode)
        ):
            raise LauncherError("Hosting mode or database provider configuration is incomplete.")
        self.database_connection_string = self._database_connection_string()
        environment = os.environ.copy()
        environment.update(
            {
                "DATABASE_PROVIDER": self.database_provider,
                "DATABASE_CONNECTION_STRING": self.database_connection_string,
                "DATABASE_PASSWORD": self.database_password,
                "API_HOSTING_MODE": "All" if self.hosting_mode == "Combined" else "Api",
            }
        )
        self.compose_env = environment
        return environment

    def _select_database_services(self) -> None:
        if self.database_provider == "SqlServer":
            self.database_service = "sqlserver"
            self.database_ready_command = (
                'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd '
                '-S localhost -U sa -C -Q "SELECT 1" -b >/dev/null 2>&1'
            )
            self.database_ready_description = "SQL Server"
        elif self.database_provider == "PostgreSQL":
            self.database_service = "postgresql"
            self.database_ready_command = "pg_isready -h localhost -U postgres -d RefTestManagement"
            self.database_ready_description = "PostgreSQL"
        elif self.database_provider == "SQLite":
            self.database_service = None
            self.database_ready_command = None
            self.database_ready_description = None
        elif self.database_provider == "MySQL":
            self.database_service = "mysql"
            self.database_ready_command = (
                'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysqladmin --protocol=tcp '
                "--host=127.0.0.1 --port=3306 --silent -u root ping"
            )
            self.database_ready_description = "MySQL"
        else:
            raise LauncherError("Unsupported database provider selection.")

    def _prepare_images(self) -> None:
        if not (self.root / "RefTestManagement.Ui" / "node_modules").is_dir():
            raise LauncherError(
                "Angular dependencies are missing. Install them in RefTestManagement.Ui before running this local setup."
            )

        self.certificate_directory.mkdir(parents=True, exist_ok=True)
        self.image_directory.mkdir(parents=True, exist_ok=True)
        self._ensure_local_https_certificate()
        self._write_local_environment()
        self._copy_api_user_secrets()
        self._ensure_required_auth0_settings()
        self._build_angular_assets()
        self._run(("dotnet", "restore", "RefTestManagement.slnx", "--locked-mode", "--nologo"))
        self._publish_images()

    def _build_angular_assets(self) -> None:
        self._run(
            "npm run build -- --configuration production",
            cwd=self.root / "RefTestManagement.Ui",
            shell=True,
            error_message="Angular production build failed.",
        )

        angular_assets = self.root / "RefTestManagement.Ui" / "dist" / "RefTestManagement.Ui" / "browser"
        if not angular_assets.is_dir():
            raise LauncherError("The Angular production build did not create its browser assets.")
        wwwroot = self.root / "RefTestManagement.Api" / "wwwroot"
        wwwroot.mkdir(parents=True, exist_ok=True)
        try:
            shutil.copytree(angular_assets, wwwroot, dirs_exist_ok=True)
        except OSError as error:
            raise LauncherError("Could not copy the Angular production assets into the API project.") from error

    def _publish_images(self) -> None:
        self._run(
            (
                "dotnet",
                "publish",
                "RefTestManagement.Api/RefTestManagement.Api.csproj",
                "--configuration",
                "Release",
                "--no-restore",
                "-t:PublishContainer",
                "-p:ContainerRuntimeIdentifier=linux-x64",
                "-p:ContainerRegistry=localhost",
                "-p:ContainerImageTag=local",
                f"-p:ContainerArchiveOutputPath={self.api_archive}",
            ),
            error_message="Could not publish the API container image.",
        )
        self._run(
            (
                "dotnet",
                "publish",
                "RefTestManagement.Worker/RefTestManagement.Worker.csproj",
                "--configuration",
                "Release",
                "--no-restore",
                "-t:PublishContainer",
                f"-p:UserSecretsId={self.user_secrets_id}",
                "-p:ContainerRuntimeIdentifier=linux-x64",
                "-p:ContainerRegistry=localhost",
                "-p:ContainerImageTag=local",
                f"-p:ContainerArchiveOutputPath={self.worker_archive}",
            ),
            error_message="Could not publish the Worker container image.",
        )

        if not self.connection_name:
            raise LauncherError("The isolated Podman connection was not initialized.")
        self.podman("--connection", self.connection_name, "load", "--input", self.api_archive)
        self.podman("--connection", self.connection_name, "load", "--input", self.worker_archive)
        self.podman(
            "--connection",
            self.connection_name,
            "image",
            "tag",
            "docker.io/library/ref-test-management-api:local",
            self.api_image,
        )
        self.podman(
            "--connection",
            self.connection_name,
            "image",
            "tag",
            "docker.io/library/ref-test-management-worker:local",
            self.worker_image,
        )
        self.podman(
            "--connection",
            self.connection_name,
            "image",
            "inspect",
            self.api_image,
            "--format",
            "{{.Id}}",
            quiet=True,
        )
        self.podman(
            "--connection",
            self.connection_name,
            "image",
            "inspect",
            self.worker_image,
            "--format",
            "{{.Id}}",
            quiet=True,
        )

    def _refresh_images(self, changed_paths: set[str]) -> None:
        if any(Path(path).parts[0] == "RefTestManagement.Ui" for path in changed_paths):
            self._build_angular_assets()
        self._publish_images()

    def _application_services(self) -> tuple[str, ...]:
        if self.hosting_mode == "Combined":
            return ("api",)
        if self.hosting_mode == "Split":
            return ("api", "worker")
        raise LauncherError("Select a supported local hosting mode.")

    def _start_compose_stack(self) -> None:
        if (
            not self.connection_name
            or not self.database_provider
            or not is_valid_hosting_mode(self.hosting_mode)
        ):
            raise LauncherError("The selected hosting mode, provider, or Podman connection is unavailable.")
        compose_env = self._configure_database()
        selected_profile = (self.database_provider,)

        self.compose(
            "config",
            "--quiet",
            profiles=selected_profile,
            env=compose_env,
            error_message="Compose configuration validation failed.",
        )
        self.compose(
            "down",
            "--remove-orphans",
            profiles=ALL_COMPOSE_PROFILES,
            env=compose_env,
        )

        if self.database_service:
            self.compose(
                "up",
                "--detach",
                self.database_service,
                "redis",
                profiles=selected_profile,
                env=compose_env,
            )
        else:
            self.compose("up", "--detach", "redis", profiles=selected_profile, env=compose_env)

        redis_container = self._compose_container_id("redis")
        if self.database_service:
            database_container = self._compose_container_id(self.database_service)
            if not self.database_ready_command or not self.database_ready_description:
                raise LauncherError("The selected database service has no readiness check.")
            self._wait_container_command(
                database_container,
                self.database_ready_command,
                self.database_ready_description,
            )
        else:
            self.compose(
                "run",
                "--rm",
                "--no-deps",
                "sqlite-init",
                profiles=selected_profile,
                env=compose_env,
                quiet=True,
                error_message="Could not initialize the SQLite volume permissions.",
            )

        self._wait_container_command(
            redis_container,
            'REDISCLI_AUTH="$REDIS_PASSWORD" redis-cli --tls --cacert /run/redis-certs/redis-ca.crt -h localhost -p 6380 PING | grep -q PONG',
            "Authenticated TLS Redis",
        )
        self.compose("up", "--detach", "migrate", env=compose_env)
        migration_container = self._compose_container_id("migrate", include_stopped=True)
        self._wait_container_completion(migration_container, "Database migration")
        application_services = self._application_services()
        self.compose("up", "--detach", *application_services, "https-proxy", env=compose_env)
        api_container = self._compose_container_id("api")
        proxy_container = self._compose_container_id("https-proxy")
        self._wait_container_running(api_container, "API")
        if "worker" in application_services:
            worker_container = self._compose_container_id("worker")
            self._wait_container_running(worker_container, "Worker")
        self._wait_container_running(proxy_container, "HTTPS proxy")
        self._remove_migration_container(compose_env)
        self.compose("ps", "--all", profiles=selected_profile, env=compose_env)

    @staticmethod
    def _changed_paths(
        previous: dict[str, tuple[int, int]],
        current: dict[str, tuple[int, int]],
    ) -> set[str]:
        return {
            path
            for path in previous.keys() | current.keys()
            if previous.get(path) != current.get(path)
        }

    def _wait_for_source_changes(
        self,
        previous: dict[str, tuple[int, int]],
    ) -> tuple[set[str], dict[str, tuple[int, int]]]:
        candidate = previous
        candidate_since: float | None = None
        while True:
            time.sleep(0.75)
            current = source_snapshot(self.root)
            if current != candidate:
                candidate = current
                candidate_since = time.monotonic() if current != previous else None
            elif candidate_since is not None and time.monotonic() - candidate_since >= 1.5:
                return self._changed_paths(previous, current), current

    def _refresh_stack(self, changed_paths: set[str]) -> None:
        if (
            not self.connection_name
            or not self.database_provider
            or not is_valid_hosting_mode(self.hosting_mode)
        ):
            raise LauncherError("The selected hosting mode, provider, or Podman connection is unavailable.")
        print(f"Source changes detected ({len(changed_paths)} file(s)); rebuilding local images...")
        self._refresh_images(changed_paths)
        compose_env = self._configure_database()
        self.compose("up", "--detach", "--force-recreate", "migrate", env=compose_env)
        migration_container = self._compose_container_id("migrate", include_stopped=True)
        self._wait_container_completion(migration_container, "Database migration after source change")

        application_services = self._application_services()
        self.compose(
            "up",
            "--detach",
            "--force-recreate",
            "--no-deps",
            *application_services,
            env=compose_env,
        )
        self._wait_container_running(self._compose_container_id("api"), "API after source change")
        if "worker" in application_services:
            self._wait_container_running(
                self._compose_container_id("worker"),
                "Worker after source change",
            )
        self._wait_for_api()
        self._remove_migration_container(compose_env)
        print("Local application containers are up to date.")

    def _watch_source(self) -> None:
        snapshot = source_snapshot(self.root)
        print("Watching application and UI source files for changes. Press Ctrl+C to stop watching.")
        try:
            while True:
                changed_paths, snapshot = self._wait_for_source_changes(snapshot)
                self._refresh_stack(changed_paths)
        except KeyboardInterrupt:
            print("\nStopping local Compose containers...")
            self.compose("down", "--remove-orphans", profiles=ALL_COMPOSE_PROFILES)
            print("Local Compose containers removed; persistent data volumes were preserved.")
            raise

    def _wait_for_api(self) -> None:
        endpoint = "https://localhost:7039/graphql?query=%7B__typename%7D"
        status = ""
        for _ in range(90):
            result = self._run(
                (
                    "curl",
                    "--silent",
                    "--show-error",
                    "--output",
                    "NUL",
                    "--write-out",
                    "%{http_code}",
                    "--max-time",
                    "5",
                    endpoint,
                ),
                capture=True,
                check=False,
            )
            status = (result.stdout or "").strip()
            if status == "200":
                return
            time.sleep(2)
        raise LauncherError(
            f"The local HTTPS endpoint did not return status 200 from its GraphQL readiness query (last probe: status {status or 'no response'}). The isolated Compose stack was left running for inspection."
        )

    def _cleanup(self) -> None:
        if self.ssh_process is not None and self.ssh_process.poll() is None:
            try:
                self.ssh_process.terminate()
                self.ssh_process.wait(timeout=2)
            except subprocess.TimeoutExpired:
                self.ssh_process.kill()
                self.ssh_process.wait(timeout=2)
            except OSError:
                print("WARNING: Could not stop the temporary Podman SSH process.", file=sys.stderr)

    def run(self) -> None:
        self.require_tools()
        self.hosting_mode = select_hosting_mode()
        self.database_provider = select_database_provider()
        self._select_database_services()
        default_connection_before = self._get_default_connection()
        self._ensure_machine()
        self.connection_name = self._find_rootless_connection()
        self._ensure_rootless_podman_api(self.connection_name)

        default_connection_after = self._get_default_connection()
        if default_connection_before and default_connection_after != default_connection_before:
            self.podman("system", "connection", "default", default_connection_before)
            if self._get_default_connection() != default_connection_before:
                raise LauncherError(
                    "Could not restore the pre-existing Podman default connection; no Compose commands were run."
                )

        self._prepare_images()
        self._ensure_redis_certificates()
        self._ensure_local_network(self.connection_name)
        self._start_compose_stack()
        self._wait_for_api()
        api_container = self._compose_container_id("api")
        self._wait_container_running(api_container, "API")
        if "worker" in self._application_services():
            worker_container = self._compose_container_id("worker")
            self._wait_container_running(worker_container, "Worker")

        print(f"{self.hosting_mode} local stack is running at https://localhost:7039.")
        print(f"{self.database_provider} database provider is active; Compose project: {self.project_name}.")
        if self.database_provider == "SqlServer":
            print("SQL Server is available on 127.0.0.1:11433.")
        elif self.database_provider == "PostgreSQL":
            print("PostgreSQL is available on 127.0.0.1:15432.")
        elif self.database_provider == "MySQL":
            print("MySQL is available on 127.0.0.1:13306.")
        else:
            print("SQLite uses a persistent Compose volume shared by the migration and application containers.")
        print("Local credentials/certificates are ignored files; values were not displayed.")
        self._watch_source()


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Build and start the isolated local Podman Compose stack."
    )
    parser.parse_args(argv)
    try:
        launcher = LocalStackLauncher()
        atexit.register(launcher._cleanup)
        launcher.run()
    except LauncherError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("ERROR: Local launcher was interrupted.", file=sys.stderr)
        return 130
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
