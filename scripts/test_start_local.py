import importlib.util
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import Mock


SCRIPT = Path(__file__).resolve().parents[1] / "infra" / "local" / "start.py"
SPEC = importlib.util.spec_from_file_location("start_local", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
launcher = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(launcher)


class LocalLauncherTests(unittest.TestCase):
    def test_selection_validation(self) -> None:
        self.assertTrue(launcher.is_valid_hosting_mode("Combined"))
        self.assertTrue(launcher.is_valid_hosting_mode("Split"))
        self.assertFalse(launcher.is_valid_hosting_mode("Worker"))
        self.assertTrue(launcher.is_valid_database_provider("SQLite"))
        self.assertFalse(launcher.is_valid_database_provider(["SQLite"]))
        self.assertFalse(launcher.is_valid_database_provider("SQL Server"))

    def test_hosting_modes_choose_the_expected_services(self) -> None:
        instance = launcher.LocalStackLauncher.__new__(launcher.LocalStackLauncher)
        instance.hosting_mode = "Combined"
        self.assertEqual(instance._application_services(), ("api",))
        instance.hosting_mode = "Split"
        self.assertEqual(instance._application_services(), ("api", "worker"))

    def test_source_snapshot_excludes_generated_files(self) -> None:
        with TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            source = root / "RefTestManagement.Api" / "Services" / "Example.cs"
            generated = root / "RefTestManagement.Api" / "bin" / "Example.dll"
            static_output = root / "RefTestManagement.Api" / "wwwroot" / "index.html"
            ui_source = root / "RefTestManagement.Ui" / "src" / "app.ts"
            dependency = root / "RefTestManagement.Ui" / "node_modules" / "pkg" / "index.js"
            for path in (source, generated, static_output, ui_source, dependency):
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("x", encoding="utf-8")

            snapshot = launcher.source_snapshot(root)

            self.assertIn(str(source.relative_to(root)), snapshot)
            self.assertIn(str(ui_source.relative_to(root)), snapshot)
            self.assertNotIn(str(generated.relative_to(root)), snapshot)
            self.assertNotIn(str(static_output.relative_to(root)), snapshot)
            self.assertNotIn(str(dependency.relative_to(root)), snapshot)

    def test_changed_paths_include_added_removed_and_updated_files(self) -> None:
        previous = {"removed.cs": (1, 1), "updated.cs": (1, 1)}
        current = {"added.cs": (1, 1), "updated.cs": (2, 1)}

        self.assertEqual(
            launcher.LocalStackLauncher._changed_paths(previous, current),
            {"removed.cs", "added.cs", "updated.cs"},
        )

    def test_ctrl_c_removes_compose_containers_and_preserves_volumes(self) -> None:
        instance = launcher.LocalStackLauncher.__new__(launcher.LocalStackLauncher)
        instance.root = Path(".")
        instance._wait_for_source_changes = Mock(side_effect=KeyboardInterrupt)
        instance.compose = Mock()

        with self.assertRaises(KeyboardInterrupt):
            instance._watch_source()

        instance.compose.assert_called_once_with(
            "down",
            "--remove-orphans",
            profiles=launcher.ALL_COMPOSE_PROFILES,
        )


if __name__ == "__main__":
    unittest.main()
