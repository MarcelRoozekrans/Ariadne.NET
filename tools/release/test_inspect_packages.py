"""Unit tests for inspect_packages.py: fake packages built with zipfile, no dotnet needed."""
import contextlib
import io
import pathlib
import tempfile
import unittest
import unittest.mock
import zipfile

import inspect_packages

VERSION = "0.1.0"
ARIADNE = "Ariadne.NET"
ABSTRACTIONS = "Ariadne.NET.Abstractions"
WORKSPACES = "Ariadne.NET.Workspaces"
TESTING = "Ariadne.NET.Testing"
ABSTRACTIONS_DEPENDENCIES = ("ZeroAlloc.Results", "ZeroAlloc.Validation")


def informational(version: str) -> bytes:
    # A custom attribute blob: prolog 01 00, a length-prefixed UTF-8 string, then 00 00 for no named arguments.
    text = version.encode("utf-8")
    return b"\x01\x00" + bytes([len(text)]) + text + b"\x00\x00"


def write_package(directory: pathlib.Path, package_id: str, version: str, *,
                  dll_version: str | None = None, nuspec_version: str | None = None,
                  dependencies: tuple[str, ...] | None = None,
                  abstractions_version: str | None = None, ariadne_version: str | None = None,
                  workspaces_version: str | None = None,
                  with_symbols: bool = True) -> None:
    # The SDK writes a project reference as a plain version, so that is what the fake nuspecs carry. Each package lists
    # its direct dependencies only: Workspaces reaches Abstractions through Ariadne.NET, and Testing reaches Ariadne.NET
    # through Workspaces, not directly.
    if dependencies is None:
        dependencies = {
            ABSTRACTIONS: ABSTRACTIONS_DEPENDENCIES,
            WORKSPACES: (ARIADNE, "Microsoft.CodeAnalysis.CSharp.Workspaces"),
            TESTING: (WORKSPACES, "Microsoft.CodeAnalysis.Workspaces.MSBuild"),
        }.get(package_id, (ABSTRACTIONS, "YamlDotNet"))
    versions = {ABSTRACTIONS: abstractions_version or version, ARIADNE: ariadne_version or version,
                WORKSPACES: workspaces_version or version}
    elements = "".join(
        f'<dependency id="{dependency}" version="{versions.get(dependency, "1.0.0")}" exclude="Build,Analyzers" />'
        for dependency in dependencies)
    nuspec = (f'<?xml version="1.0" encoding="utf-8"?><package><metadata><id>{package_id}</id>'
              f'<version>{nuspec_version or version}</version>'
              f'<dependencies><group targetFramework="net10.0">{elements}</group></dependencies>'
              f'</metadata></package>')
    # FileVersion is a four-part string that must never count as the informational version.
    dll = b"MZ fake " + b"\x01\x00\x070.0.0.0\x00\x00" + informational(dll_version or (version + "+abc1234"))
    with zipfile.ZipFile(directory / f"{package_id}.{version}.nupkg", "w") as archive:
        archive.writestr(f"{package_id}.nuspec", nuspec)
        archive.writestr(f"lib/net10.0/{package_id}.dll", dll)
    if with_symbols:
        with zipfile.ZipFile(directory / f"{package_id}.{version}.snupkg", "w") as archive:
            archive.writestr(f"lib/net10.0/{package_id}.pdb", b"pdb")


class InspectPackagesTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp = tempfile.TemporaryDirectory()
        self.directory = pathlib.Path(self._temp.name)

    def tearDown(self) -> None:
        self._temp.cleanup()

    def write_all(self, version: str = VERSION, **overrides: dict) -> None:
        for package_id in inspect_packages.PACKAGES:
            write_package(self.directory, package_id, version, **overrides.get(package_id, {}))

    def test_accepts_the_four_release_packages(self) -> None:
        self.write_all()
        self.assertEqual([], inspect_packages.problems(self.directory, VERSION))

    def test_rejects_a_fifth_package(self) -> None:
        self.write_all()
        write_package(self.directory, "Ariadne.NET.Other", VERSION)
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any("exactly the four release packages" in error and "Ariadne.NET.Other.0.1.0.nupkg" in error
                            for error in errors), errors)

    def test_rejects_a_missing_testing_package(self) -> None:
        self.write_all()
        (self.directory / f"{TESTING}.{VERSION}.nupkg").unlink()
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(f"{TESTING}.0.1.0.nupkg" in error and "missing" in error for error in errors), errors)

    def test_accepts_testing_depending_on_msbuild_packages(self) -> None:
        self.write_all(**{TESTING: {"dependencies": (WORKSPACES, "Microsoft.Build.Framework",
                                                      "Microsoft.CodeAnalysis.Workspaces.MSBuild")}})
        self.assertEqual([], inspect_packages.problems(self.directory, VERSION))

    def test_rejects_testing_depending_on_another_workspaces_version(self) -> None:
        self.write_all(**{TESTING: {"workspaces_version": "0.0.9"}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(TESTING in error and f"depends on {WORKSPACES} 0.0.9, expected {VERSION}" in error
                            for error in errors), errors)

    def test_rejects_testing_without_the_workspaces_dependency(self) -> None:
        self.write_all(**{TESTING: {"dependencies": ("Microsoft.CodeAnalysis.Workspaces.MSBuild",)}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(f"{TESTING} does not depend on {WORKSPACES}" in error for error in errors), errors)

    def test_rejects_testing_depending_on_the_engine_directly(self) -> None:
        self.write_all(**{TESTING: {"dependencies": (WORKSPACES, ARIADNE)}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(f"{TESTING} depends on {ARIADNE}" in error for error in errors), errors)

    def test_rejects_the_abstractions_depending_on_msbuild_packages(self) -> None:
        self.write_all(**{ABSTRACTIONS: {"dependencies": (*ABSTRACTIONS_DEPENDENCIES, "Microsoft.Build.Locator")}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(f"{ABSTRACTIONS} depends on Microsoft.Build.Locator" in error and "MSBuild" in error
                            for error in errors), errors)

    def test_rejects_a_missing_workspaces_package(self) -> None:
        self.write_all()
        (self.directory / f"{WORKSPACES}.{VERSION}.nupkg").unlink()
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(f"{WORKSPACES}.0.1.0.nupkg" in error and "missing" in error for error in errors), errors)

    def test_rejects_the_engine_depending_on_msbuild_packages(self) -> None:
        for msbuild in ("Microsoft.Build.Locator", "Microsoft.Build.Framework", "Microsoft.CodeAnalysis.Workspaces.MSBuild"):
            with self.subTest(msbuild):
                self.write_all(**{ARIADNE: {"dependencies": (ABSTRACTIONS, msbuild)}})
                errors = inspect_packages.problems(self.directory, VERSION)
                self.assertTrue(any(f"{ARIADNE} depends on {msbuild}" in error and "MSBuild" in error
                                    for error in errors), errors)

    def test_accepts_workspaces_depending_on_msbuild_packages(self) -> None:
        self.write_all(**{WORKSPACES: {"dependencies": (ARIADNE, "Microsoft.Build.Locator",
                                                         "Microsoft.CodeAnalysis.Workspaces.MSBuild")}})
        self.assertEqual([], inspect_packages.problems(self.directory, VERSION))

    def test_rejects_workspaces_depending_on_another_engine_version(self) -> None:
        self.write_all(**{WORKSPACES: {"ariadne_version": "0.0.9"}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(WORKSPACES in error and f"depends on {ARIADNE} 0.0.9, expected {VERSION}" in error
                            for error in errors), errors)

    def test_rejects_workspaces_without_the_engine_dependency(self) -> None:
        self.write_all(**{WORKSPACES: {"dependencies": ("Microsoft.CodeAnalysis.CSharp.Workspaces",)}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(f"{WORKSPACES} does not depend on {ARIADNE}" in error for error in errors), errors)

    def test_rejects_workspaces_depending_on_abstractions_directly(self) -> None:
        self.write_all(**{WORKSPACES: {"dependencies": (ARIADNE, ABSTRACTIONS)}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(f"{WORKSPACES} depends on {ABSTRACTIONS}" in error for error in errors), errors)

    def test_rejects_a_missing_snupkg(self) -> None:
        self.write_all(**{ABSTRACTIONS: {"with_symbols": False}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(ABSTRACTIONS in error and ".snupkg" in error for error in errors), errors)

    def test_rejects_an_abstractions_dependency_beyond_results_and_validation(self) -> None:
        self.write_all(**{ABSTRACTIONS: {"dependencies": (*ABSTRACTIONS_DEPENDENCIES, "YamlDotNet")}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(ABSTRACTIONS in error and "YamlDotNet" in error for error in errors), errors)

    def test_rejects_an_abstractions_dependency_that_is_missing(self) -> None:
        self.write_all(**{ABSTRACTIONS: {"dependencies": ("ZeroAlloc.Results",)}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(ABSTRACTIONS in error and "ZeroAlloc.Validation" in error for error in errors), errors)

    def test_rejects_ariadne_depending_on_another_abstractions_version(self) -> None:
        self.write_all(**{ARIADNE: {"abstractions_version": "0.0.9"}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(ARIADNE in error and "0.0.9" in error for error in errors), errors)

    def test_rejects_ariadne_without_the_abstractions_dependency(self) -> None:
        self.write_all(**{ARIADNE: {"dependencies": ("YamlDotNet",)}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(f"{ARIADNE} does not depend on {ABSTRACTIONS}" in error for error in errors), errors)

    def test_rejects_a_dll_that_says_local(self) -> None:
        self.write_all(**{ARIADNE: {"dll_version": "0.1.0-local+abc"}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(ARIADNE in error and "AriadneRelease" in error for error in errors), errors)

    def test_names_the_version_a_local_dll_says(self) -> None:
        self.write_all(**{ARIADNE: {"dll_version": "0.1.0-local+abc"}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any("says 0.1.0-local+abc, expected 0.1.0" in error and "AriadneRelease" in error
                            for error in errors), errors)

    def test_names_the_version_when_the_length_byte_is_a_digit(self) -> None:
        # 52 characters, so the length byte is the digit 4, as with a real commit hash.
        local = "0.1.0-local+" + "a" * 40
        self.write_all(**{ARIADNE: {"dll_version": local}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(f"says {local}, expected 0.1.0" in error for error in errors), errors)

    def test_accepts_a_bare_version_without_a_suffix(self) -> None:
        self.write_all(**{ARIADNE: {"dll_version": "0.1.0"}})
        self.assertEqual([], inspect_packages.problems(self.directory, VERSION))

    def test_rejects_a_version_that_extends_the_last_number(self) -> None:
        self.write_all(**{ARIADNE: {"dll_version": "0.1.01"}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(ARIADNE in error and "says 0.1.01, expected 0.1.0" in error for error in errors), errors)

    def test_rejects_a_local_prerelease_without_a_commit_suffix(self) -> None:
        self.write_all(**{ARIADNE: {"dll_version": "0.1.0-local"}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(ARIADNE in error and "says 0.1.0-local, expected 0.1.0" in error for error in errors), errors)

    def test_rejects_a_symbol_package_without_a_nupkg(self) -> None:
        self.write_all()
        write_package(self.directory, "Other", VERSION)
        (self.directory / f"Other.{VERSION}.nupkg").unlink()
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any("Other.0.1.0.snupkg" in error and "no matching" in error for error in errors), errors)

    def test_a_longer_version_ending_in_the_release_does_not_count(self) -> None:
        self.write_all(**{ARIADNE: {"dll_version": "10.1.0"}})
        self.assertNotEqual([], inspect_packages.problems(self.directory, VERSION))

    def test_rejects_a_nuspec_at_another_version(self) -> None:
        self.write_all(**{ABSTRACTIONS: {"nuspec_version": "0.1.1"}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(ABSTRACTIONS in error and "nuspec version is 0.1.1" in error for error in errors), errors)

    def test_rejects_a_package_without_a_dll(self) -> None:
        self.write_all()
        path = self.directory / f"{ARIADNE}.{VERSION}.nupkg"
        with zipfile.ZipFile(path) as source:
            nuspec = source.read(f"{ARIADNE}.nuspec")
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr(f"{ARIADNE}.nuspec", nuspec)
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(ARIADNE in error and "no DLL under lib/" in error for error in errors), errors)

    def test_rejects_an_invalid_symbol_package(self) -> None:
        self.write_all()
        (self.directory / f"{ARIADNE}.{VERSION}.snupkg").write_bytes(b"")
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(ARIADNE in error and "not a valid symbol package" in error for error in errors), errors)

    def test_rejects_a_symbol_package_without_the_pdb(self) -> None:
        self.write_all()
        buffer = io.BytesIO()
        with zipfile.ZipFile(buffer, "w") as archive:
            archive.writestr("readme.txt", b"x")
        (self.directory / f"{ARIADNE}.{VERSION}.snupkg").write_bytes(buffer.getvalue())
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(ARIADNE in error and f"has no {ARIADNE}.pdb" in error for error in errors), errors)

    def test_main_reports_usage(self) -> None:
        with contextlib.redirect_stderr(io.StringIO()) as stderr:
            self.assertEqual(2, inspect_packages.main([]))
        self.assertIn("usage:", stderr.getvalue())

    def test_main_prints_one_error_line_per_problem(self) -> None:
        self.write_all(**{ARIADNE: {"dll_version": "0.1.0-local+abc"}})
        with unittest.mock.patch("builtins.print") as printed:
            self.assertEqual(1, inspect_packages.main([str(self.directory), VERSION]))
        lines = [call.args[0] for call in printed.call_args_list]
        self.assertTrue(lines and all(line.startswith("::error::") for line in lines), lines)


if __name__ == "__main__":
    unittest.main()
