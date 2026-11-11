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
CLI = "Ariadne.NET.Cli"
RELEASE_PACKAGES = (ABSTRACTIONS, ARIADNE, WORKSPACES, TESTING, CLI)
TOOL = "tools/net10.0/any/"
# What a real pack of the tool bundles from the MSBuild side: MSBuildLocator at the top and in both build host folders.
TOOL_MSBUILD_FILES = (
    f"{TOOL}Microsoft.Build.Locator.dll",
    f"{TOOL}Microsoft.CodeAnalysis.Workspaces.MSBuild.dll",
    f"{TOOL}BuildHost-netcore/Microsoft.Build.Locator.dll",
    f"{TOOL}BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll",
    f"{TOOL}BuildHost-net472/Microsoft.Build.Locator.dll",
    f"{TOOL}BuildHost-net472/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.exe",
)
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


def tool_settings(command: str = "ariadne", entry_point: str = f"{CLI}.dll", runner: str = "dotnet") -> str:
    return (f'\ufeff<?xml version="1.0" encoding="utf-8"?><DotNetCliTool Version="1"><Commands>'
            f'<Command Name="{command}" EntryPoint="{entry_point}" Runner="{runner}" />'
            f'</Commands></DotNetCliTool>')


def write_tool(directory: pathlib.Path, version: str, *,
               dll_version: str | None = None, engine_dll_version: str | None = None,
               nuspec_version: str | None = None, dependencies: tuple[str, ...] = (),
               package_type: str | None = "DotnetTool", settings: str | None = None,
               extra_files: tuple[str, ...] = (), with_symbols: bool = True) -> None:
    # A tool bundles everything under tools/<framework>/any/, so its nuspec lists no dependencies and has no lib/.
    elements = "".join(f'<dependency id="{dependency}" version="{version}" />' for dependency in dependencies)
    dependency_group = f"<dependencies><group targetFramework=\"net10.0\">{elements}</group></dependencies>" if elements else ""
    package_types = f'<packageTypes><packageType name="{package_type}" /></packageTypes>' if package_type else ""
    nuspec = (f'<?xml version="1.0" encoding="utf-8"?><package><metadata><id>{CLI}</id>'
              f'<version>{nuspec_version or version}</version>{package_types}{dependency_group}'
              f'</metadata></package>')

    def dll(informational_version: str) -> bytes:
        return b"MZ fake " + b"\x01\x00\x070.0.0.0\x00\x00" + informational(informational_version)

    with zipfile.ZipFile(directory / f"{CLI}.{version}.nupkg", "w") as archive:
        archive.writestr(f"{CLI}.nuspec", nuspec)
        archive.writestr(f"{TOOL}DotnetToolSettings.xml", settings if settings is not None else tool_settings())
        archive.writestr(f"{TOOL}{CLI}.dll", dll(dll_version or (version + "+abc1234")))
        for bundled in (ARIADNE, ABSTRACTIONS, WORKSPACES, "Ariadne.NET.Hooks"):
            archive.writestr(f"{TOOL}{bundled}.dll", dll(engine_dll_version or (version + "+abc1234")))
        # Third-party DLLs carry their own versions, which the check must leave alone.
        archive.writestr(f"{TOOL}YamlDotNet.dll", dll("16.3.0+deadbeef"))
        for name in (*TOOL_MSBUILD_FILES, *extra_files):
            archive.writestr(name, b"MZ")
    if with_symbols:
        with zipfile.ZipFile(directory / f"{CLI}.{version}.snupkg", "w") as archive:
            archive.writestr(f"{TOOL}{CLI}.pdb", b"pdb")


class InspectPackagesTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp = tempfile.TemporaryDirectory()
        self.directory = pathlib.Path(self._temp.name)

    def tearDown(self) -> None:
        self._temp.cleanup()

    def write_all(self, version: str = VERSION, **overrides: dict) -> None:
        for package_id in RELEASE_PACKAGES:
            if package_id == CLI:
                write_tool(self.directory, version, **overrides.get(package_id, {}))
            else:
                write_package(self.directory, package_id, version, **overrides.get(package_id, {}))

    def test_accepts_the_five_release_packages(self) -> None:
        self.write_all()
        self.assertEqual([], inspect_packages.problems(self.directory, VERSION))

    def test_rejects_a_sixth_package(self) -> None:
        self.write_all()
        write_package(self.directory, "Ariadne.NET.Other", VERSION)
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any("exactly the five release packages" in error and "Ariadne.NET.Other.0.1.0.nupkg" in error
                            for error in errors), errors)

    def test_rejects_a_missing_tool_package(self) -> None:
        self.write_all()
        (self.directory / f"{CLI}.{VERSION}.nupkg").unlink()
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(f"{CLI}.0.1.0.nupkg" in error and "missing" in error for error in errors), errors)

    def test_rejects_a_tool_that_is_not_a_dotnet_tool_package(self) -> None:
        for package_type in (None, "Dependency"):
            with self.subTest(package_type):
                self.write_all(**{CLI: {"package_type": package_type}})
                errors = inspect_packages.problems(self.directory, VERSION)
                self.assertTrue(any(CLI in error and "DotnetTool" in error for error in errors), errors)

    def test_rejects_a_tool_without_its_settings(self) -> None:
        self.write_all()
        path = self.directory / f"{CLI}.{VERSION}.nupkg"
        with zipfile.ZipFile(path) as source:
            entries = {name: source.read(name) for name in source.namelist()
                       if not name.endswith("DotnetToolSettings.xml")}
        with zipfile.ZipFile(path, "w") as archive:
            for name, content in entries.items():
                archive.writestr(name, content)
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(CLI in error and f"{TOOL}DotnetToolSettings.xml" in error and "missing" in error
                            for error in errors), errors)

    def test_rejects_a_tool_with_another_command_name(self) -> None:
        self.write_all(**{CLI: {"settings": tool_settings(command="ariadne-cli")}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(CLI in error and "ariadne-cli" in error and "expected ariadne" in error
                            for error in errors), errors)

    def test_rejects_a_tool_whose_entry_point_is_not_bundled(self) -> None:
        self.write_all(**{CLI: {"settings": tool_settings(entry_point="ariadne.dll")}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(CLI in error and "ariadne.dll" in error for error in errors), errors)

    def test_rejects_a_tool_with_another_runner(self) -> None:
        self.write_all(**{CLI: {"settings": tool_settings(runner="executable")}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(CLI in error and "executable" in error and "expected dotnet" in error
                            for error in errors), errors)

    def test_rejects_a_tool_with_an_ariadne_dependency(self) -> None:
        for dependency in (ARIADNE, WORKSPACES):
            with self.subTest(dependency):
                self.write_all(**{CLI: {"dependencies": (dependency,)}})
                errors = inspect_packages.problems(self.directory, VERSION)
                self.assertTrue(any(f"{CLI} depends on {dependency}" in error for error in errors), errors)

    def test_rejects_a_tool_depending_on_msbuild_packages(self) -> None:
        self.write_all(**{CLI: {"dependencies": ("Microsoft.Build.Locator",)}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(f"{CLI} depends on Microsoft.Build.Locator" in error and "MSBuild" in error
                            for error in errors), errors)

    def test_rejects_a_tool_bundling_msbuild_assemblies(self) -> None:
        for bundled in (f"{TOOL}Microsoft.Build.dll", f"{TOOL}Microsoft.Build.Framework.dll",
                        f"{TOOL}Microsoft.Build.Tasks.Core.dll", f"{TOOL}BuildHost-netcore/Microsoft.Build.dll",
                        f"{TOOL}BuildHost-net472/Microsoft.Build.Framework.dll", f"{TOOL}de/Microsoft.Build.resources.dll"):
            with self.subTest(bundled):
                self.write_all(**{CLI: {"extra_files": (bundled,)}})
                errors = inspect_packages.problems(self.directory, VERSION)
                self.assertTrue(any(CLI in error and bundled in error and "MSBuild" in error for error in errors), errors)

    def test_accepts_msbuild_locator_in_the_tool_and_its_build_hosts(self) -> None:
        # write_tool already bundles the files a real pack has; this pins that they pass.
        self.write_all()
        with zipfile.ZipFile(self.directory / f"{CLI}.{VERSION}.nupkg") as archive:
            names = set(archive.namelist())
        self.assertTrue(set(TOOL_MSBUILD_FILES) <= names)
        self.assertEqual([], inspect_packages.problems(self.directory, VERSION))

    def test_rejects_a_tool_built_without_the_release_switch(self) -> None:
        self.write_all(**{CLI: {"dll_version": "0.1.0-local+abc"}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(CLI in error and f"{TOOL}{CLI}.dll says 0.1.0-local+abc, expected 0.1.0" in error
                            and "AriadneRelease" in error for error in errors), errors)

    def test_rejects_a_tool_bundling_an_engine_at_another_version(self) -> None:
        self.write_all(**{CLI: {"engine_dll_version": "0.0.9+abc"}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(CLI in error and f"{TOOL}{WORKSPACES}.dll says 0.0.9+abc" in error for error in errors), errors)

    def test_rejects_a_tool_without_its_main_dll(self) -> None:
        self.write_all()
        path = self.directory / f"{CLI}.{VERSION}.nupkg"
        with zipfile.ZipFile(path) as source:
            entries = {name: source.read(name) for name in source.namelist() if name != f"{TOOL}{CLI}.dll"}
        with zipfile.ZipFile(path, "w") as archive:
            for name, content in entries.items():
                archive.writestr(name, content)
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(CLI in error and f"{TOOL}{CLI}.dll" in error and "missing" in error for error in errors),
                        errors)

    def test_rejects_a_tool_without_a_bundled_ariadne_assembly(self) -> None:
        for bundled in (ARIADNE, ABSTRACTIONS, WORKSPACES, "Ariadne.NET.Hooks"):
            with self.subTest(bundled):
                self.write_all()
                path = self.directory / f"{CLI}.{VERSION}.nupkg"
                with zipfile.ZipFile(path) as source:
                    entries = {name: source.read(name) for name in source.namelist()
                               if name != f"{TOOL}{bundled}.dll"}
                with zipfile.ZipFile(path, "w") as archive:
                    for name, content in entries.items():
                        archive.writestr(name, content)
                errors = inspect_packages.problems(self.directory, VERSION)
                self.assertTrue(any(CLI in error and f"{TOOL}{bundled}.dll is missing" in error for error in errors),
                                errors)

    def test_rejects_a_tool_at_another_nuspec_version(self) -> None:
        self.write_all(**{CLI: {"nuspec_version": "0.1.1"}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(CLI in error and "nuspec version is 0.1.1" in error for error in errors), errors)

    def test_rejects_a_tool_without_symbols(self) -> None:
        self.write_all(**{CLI: {"with_symbols": False}})
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(CLI in error and f"{CLI}.0.1.0.snupkg is missing" in error for error in errors), errors)

    def test_rejects_a_tool_symbol_package_without_the_pdb(self) -> None:
        self.write_all()
        with zipfile.ZipFile(self.directory / f"{CLI}.{VERSION}.snupkg", "w") as archive:
            archive.writestr(f"{TOOL}{ARIADNE}.pdb", b"pdb")
        errors = inspect_packages.problems(self.directory, VERSION)
        self.assertTrue(any(CLI in error and f"has no {CLI}.pdb" in error for error in errors), errors)

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
