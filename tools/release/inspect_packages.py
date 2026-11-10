"""Checks the packages a release is about to push to nuget.org, before anything is pushed.

Usage: python3 tools/release/inspect_packages.py <directory> <version>

A NuGet version can be unlisted but never deleted, so ci.yml runs this in pack-validate on every push and again in
publish-nuget just before the push. It fails when the directory does not hold exactly the five release packages,
Ariadne.NET, Ariadne.NET.Abstractions, Ariadne.NET.Workspaces, Ariadne.NET.Testing and the dotnet tool
Ariadne.NET.Cli, at the release version, each with its .snupkg symbol package; when an Ariadne DLL was built without
-p:AriadneRelease=true, which Directory.Build.props versions <release>-local and which is compiled into the DLL;
when Ariadne.NET.Abstractions depends on anything but ZeroAlloc.Results and ZeroAlloc.Validation; when Ariadne.NET
depends on Ariadne.NET.Abstractions at a different version, or when Ariadne.NET, Ariadne.NET.Abstractions or the
tool depends on any MSBuild package, since only Ariadne.NET.Workspaces and Ariadne.NET.Testing may reference
MSBuild; when Ariadne.NET.Workspaces depends on Ariadne.NET at a different version; when Ariadne.NET.Testing depends
on Ariadne.NET.Workspaces at a different version; or when a package lists an Ariadne dependency it does not declare.
The tool bundles its dependencies, so it lists no Ariadne dependency at all; it also fails when it is not a
DotnetTool package whose DotnetToolSettings.xml runs the bundled Ariadne.NET.Cli.dll with dotnet as the ariadne
command, or when it bundles any Microsoft.Build assembly but Microsoft.Build.Locator.dll, because MSBuildLocator
must load MSBuild from the .NET SDK. Other third-party dependencies are not checked. Prints one ::error:: line per
problem, which GitHub Actions shows as an annotation.
Standard library only.
"""
import pathlib
import re
import sys
import xml.etree.ElementTree as ElementTree
import zipfile

# Each package and the Ariadne packages it must depend on, from the ProjectReferences in its csproj.
PACKAGES: dict[str, tuple[str, ...]] = {
    "Ariadne.NET": ("Ariadne.NET.Abstractions",),
    "Ariadne.NET.Abstractions": (),
    # Abstractions reaches Workspaces only through Ariadne.NET, so it is not a direct dependency.
    "Ariadne.NET.Workspaces": ("Ariadne.NET",),
    # Testing reaches the engine and Abstractions only through Workspaces.
    "Ariadne.NET.Testing": ("Ariadne.NET.Workspaces",),
    # The tool bundles the engine, Workspaces and Hooks under its tools folder instead of depending on the packages.
    "Ariadne.NET.Cli": (),
}

# Abstractions stays dependency-light so a rule-pack author can reference it without the engine: nothing else may creep in.
ABSTRACTIONS = "Ariadne.NET.Abstractions"
ABSTRACTIONS_DEPENDENCIES = {"ZeroAlloc.Results", "ZeroAlloc.Validation"}

# Only Ariadne.NET.Workspaces and Ariadne.NET.Testing may reference MSBuild: the engine must stay loadable without it,
# and the CLI and MCP server must not carry Microsoft.Build assemblies. Testing loads solutions through Workspaces and
# needs the MSBuild BuildHost folders. The name prefix Microsoft.Build covers Microsoft.Build.Locator too.
MSBUILD_PACKAGES = {"Ariadne.NET.Workspaces", "Ariadne.NET.Testing"}
MSBUILD_PREFIXES = ("Microsoft.Build", "Microsoft.CodeAnalysis.Workspaces.MSBuild")

# The dotnet tool: everything it runs sits in one framework-dependent folder, described by DotnetToolSettings.xml.
TOOL = "Ariadne.NET.Cli"
TOOL_COMMAND = "ariadne"
TOOL_RUNNER = "dotnet"
TOOL_FOLDER = "tools/net10.0/any/"
# MSBuildLocator loads MSBuild from the .NET SDK, so the tool must not bundle a copy of any Microsoft.Build assembly,
# satellite resources included. A real pack holds only Microsoft.Build.Locator.dll, at the top of the tools folder and
# in the BuildHost-netcore and BuildHost-net472 folders that Microsoft.CodeAnalysis.Workspaces.MSBuild ships, where
# the out-of-process build host locates MSBuild the same way.
TOOL_MSBUILD_ALLOWED = {"Microsoft.Build.Locator.dll"}


def _informational_version_found(dll: bytes, version: str) -> bool:
    # AssemblyInformationalVersion is stored as a length-prefixed UTF-8 string followed by 00 00: the release version,
    # optionally followed by +<commit>. The four-part FileVersion, 1.0.0.0, never matches, because a '.' follows the
    # version there. The byte before the string must be its length, which rejects a longer version that merely ends in
    # this one, such as 11.0.0 for 1.0.0.
    pattern = re.escape(version.encode("utf-8")) + rb"(?:\+[0-9A-Za-z.]+)?(?=\x00\x00)"
    return any(
        match.start() > 0 and dll[match.start() - 1] == len(match.group())
        for match in re.finditer(pattern, dll)
    )


def _informational_versions_seen(dll: bytes) -> list[str]:
    # Version-like length-prefixed strings that end a custom attribute blob, for the error message. The four-part
    # FileVersion is skipped: it is never the informational version.
    # A lookahead finds overlapping candidates: the length byte can itself be a digit, as '4' is for 52 characters.
    seen: list[str] = []
    for match in re.finditer(rb"(?=([0-9]+\.[0-9]+\.[0-9]+[0-9A-Za-z.+-]*)\x00\x00)", dll):
        text = match.group(1).decode("ascii")
        if match.start() > 0 and dll[match.start() - 1] == len(text) and not re.fullmatch(r"[0-9]+(\.[0-9]+){3}", text):
            seen.append(text)
    return seen


def _symbol_problems(directory: pathlib.Path, package: str, version: str) -> list[str]:
    path = directory / f"{package}.{version}.snupkg"
    if not path.is_file():
        return [f"{package}: {path.name} is missing"]
    try:
        with zipfile.ZipFile(path) as archive:
            if not any(name.endswith(f"{package}.pdb") for name in archive.namelist()):
                return [f"{package}: {path.name} has no {package}.pdb"]
    except zipfile.BadZipFile as exc:
        return [f"{package}: {path.name} is not a valid symbol package: {exc}"]
    return []


def _is_bundled_ariadne_dll(name: str) -> bool:
    file = name.removeprefix(TOOL_FOLDER)
    return name.startswith(TOOL_FOLDER) and "/" not in file and file.startswith("Ariadne.NET") and file.endswith(".dll")


def _tool_problems(nuspec: str, names: list[str], settings: bytes | None) -> list[str]:
    errors: list[str] = []
    package_types = re.findall(r'<packageType\b[^>]*\bname="([^"]+)"', nuspec)
    if "DotnetTool" not in package_types:
        errors.append(f"{TOOL}: nuspec package type is {', '.join(package_types) or 'missing'}, expected DotnetTool")

    if f"{TOOL_FOLDER}{TOOL}.dll" not in names:
        errors.append(f"{TOOL}: {TOOL_FOLDER}{TOOL}.dll is missing")

    if settings is None:
        errors.append(f"{TOOL}: {TOOL_FOLDER}DotnetToolSettings.xml is missing")
    else:
        try:
            commands = ElementTree.fromstring(settings).findall("./Commands/Command")
        except ElementTree.ParseError as exc:
            commands = []
            errors.append(f"{TOOL}: {TOOL_FOLDER}DotnetToolSettings.xml is not valid XML: {exc}")
        else:
            if len(commands) != 1:
                errors.append(f"{TOOL}: DotnetToolSettings.xml has {len(commands)} commands, expected one")
        for command in commands[:1]:
            name, entry_point, runner = (command.get(key) for key in ("Name", "EntryPoint", "Runner"))
            if name != TOOL_COMMAND:
                errors.append(f"{TOOL}: the command is named {name}, expected {TOOL_COMMAND}")
            if runner != TOOL_RUNNER:
                errors.append(f"{TOOL}: the command's runner is {runner}, expected {TOOL_RUNNER}")
            if not entry_point or f"{TOOL_FOLDER}{entry_point}" not in names:
                errors.append(f"{TOOL}: the command's entry point {entry_point} is not in {TOOL_FOLDER}")

    for name in sorted(names):
        file = name.rsplit("/", 1)[-1]
        if file.startswith("Microsoft.Build.") and file not in TOOL_MSBUILD_ALLOWED:
            errors.append(f"{TOOL}: bundles {name}; MSBuild must come from the .NET SDK, and only "
                          f"Microsoft.Build.Locator.dll may ship")
    return errors


def _dependencies(nuspec: str) -> dict[str, list[str]]:
    found: dict[str, list[str]] = {}
    for tag in re.findall(r"<dependency\b[^>]*>", nuspec):
        identity = re.search(r'\bid="([^"]+)"', tag)
        if identity is None:
            continue
        dependency_version = re.search(r'\bversion="([^"]+)"', tag)
        found.setdefault(identity.group(1), []).append(dependency_version.group(1) if dependency_version else "(no version)")
    return found


def _package_problems(directory: pathlib.Path, package: str, version: str) -> list[str]:
    errors: list[str] = []
    path = directory / f"{package}.{version}.nupkg"
    try:
        with zipfile.ZipFile(path) as archive:
            nuspec = archive.read(f"{package}.nuspec").decode("utf-8")
            names = archive.namelist()
            # A library's DLLs are its own build. The tool's top folder also holds third-party DLLs with their own
            # versions, so only the Ariadne assemblies, all built in the same pack, are checked there.
            dlls = {name: archive.read(name) for name in names
                    if (_is_bundled_ariadne_dll(name) if package == TOOL
                        else name.startswith("lib/") and name.endswith(".dll"))}
            settings = archive.read(f"{TOOL_FOLDER}DotnetToolSettings.xml") \
                if package == TOOL and f"{TOOL_FOLDER}DotnetToolSettings.xml" in names else None
    except zipfile.BadZipFile as exc:
        return [f"{package}: {path.name} is not a valid package: {exc}"]
    except KeyError:
        return [f"{package}: {path.name} has no {package}.nuspec"]

    declared_id = re.search(r"<id>([^<]+)</id>", nuspec)
    if declared_id is None or declared_id.group(1) != package:
        errors.append(f"{package}: nuspec id is {declared_id.group(1) if declared_id else 'missing'}, expected {package}")

    declared = re.search(r"<version>([^<]+)</version>", nuspec)
    if declared is None or declared.group(1) != version:
        errors.append(f"{package}: nuspec version is {declared.group(1) if declared else 'missing'}, expected {version}")

    if package == TOOL:
        errors.extend(_tool_problems(nuspec, names, settings))
    elif not dlls:
        errors.append(f"{package}: {path.name} has no DLL under lib/")
    for name, content in sorted(dlls.items()):
        if not _informational_version_found(content, version):
            seen = _informational_versions_seen(content)
            says = f"says {', '.join(seen)}, expected {version}" if seen else f"has no informational version {version}"
            errors.append(f"{package}: {name} {says}; was it built with -p:AriadneRelease=true?")

    errors.extend(_symbol_problems(directory, package, version))

    found = _dependencies(nuspec)
    if package == ABSTRACTIONS:
        for dependency in sorted(ABSTRACTIONS_DEPENDENCIES - found.keys()):
            errors.append(f"{package} does not depend on {dependency}")
        for dependency in sorted(found.keys() - ABSTRACTIONS_DEPENDENCIES):
            errors.append(f"{package} depends on {dependency}, which is not one of ZeroAlloc.Results and ZeroAlloc.Validation")

    if package not in MSBUILD_PACKAGES:
        for dependency in sorted(found):
            if dependency.startswith(MSBUILD_PREFIXES):
                errors.append(f"{package} depends on {dependency}; only Ariadne.NET.Workspaces and Ariadne.NET.Testing may reference MSBuild")

    # The SDK writes a project reference as the plain package version, so that is the one form accepted.
    for dependency in PACKAGES[package]:
        if dependency not in found:
            errors.append(f"{package} does not depend on {dependency}, expected {version}")
    for dependency, versions in found.items():
        if dependency.startswith("Ariadne.NET") and dependency not in PACKAGES[package]:
            errors.append(f"{package} depends on {dependency}, which is not one of its expected Ariadne dependencies")
        if dependency in PACKAGES[package]:
            for dependency_version in versions:
                if dependency_version != version:
                    errors.append(f"{package} depends on {dependency} {dependency_version}, expected {version}")
    return errors


def problems(directory: pathlib.Path, version: str) -> list[str]:
    found = sorted(path.name for path in directory.glob("*.nupkg"))
    expected = sorted(f"{package}.{version}.nupkg" for package in PACKAGES)
    if found != expected:
        missing = sorted(set(expected) - set(found))
        extra = sorted(set(found) - set(expected))
        return [f"expected exactly the five release packages in {directory}; missing {missing}, unexpected {extra}"]

    errors: list[str] = []
    expected_symbols = {f"{package}.{version}.snupkg" for package in PACKAGES}
    for name in sorted(path.name for path in directory.glob("*.snupkg")):
        if name not in expected_symbols:
            errors.append(f"{name} has no matching release package")
    for package in PACKAGES:
        errors.extend(_package_problems(directory, package, version))
    return errors


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print("usage: inspect_packages.py <directory> <version>", file=sys.stderr)
        return 2
    directory, version = pathlib.Path(argv[0]), argv[1]
    errors = problems(directory, version)
    for error in errors:
        print(f"::error::{error}")
    if errors:
        return 1
    print(f"OK: {', '.join(f'{package} {version}' for package in PACKAGES)} are ready to push")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
