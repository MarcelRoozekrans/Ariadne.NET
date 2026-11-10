---
sidebar_position: 14
---

# Reports

`ariadne validate` prints a text report by default. It can also write the same result as JSON or as SARIF 2.1.0, for tools that read a report instead of a terminal. The writers live in `Ariadne.NET` (`Reports/`) and serialize with ZeroAlloc.Serialisation, so a host or a test can call them without the CLI.

## Command line

```bash
ariadne validate --format json
ariadne validate --format sarif --output reports/architecture.sarif
ariadne validate --output reports/architecture.txt
```

| Option | Values | Meaning |
|--------|--------|---------|
| `--format` | `text` (default), `json`, `sarif` | The report format. |
| `--output` | a file path | Write the report to this file. Its folder is created when missing and an existing file is overwritten. |

Where the output goes:

| Command | Standard output | Standard error | File |
|---------|-----------------|----------------|------|
| `--format text` | the text report | | |
| `--format text --output f` | | | the text report |
| `--format json` or `sarif` | the report, alone | the whole text report | |
| `--format json` or `sarif --output f` | the text report | | the report |

With `json` or `sarif` and no `--output`, standard output holds only the report, so a pipe can parse it. The text report, with the summary and every violation, goes to standard error.

Exit codes are the same for every format: 0 when there are no violations, 1 when there are, 2 for an error. A report that cannot be written (a file that cannot be created, a closed standard output) is an error: the command prints `Error: The report for '<target>' could not be written.` with the reason on the next line, and exits 2.

An unknown `--format` value is a usage error. System.CommandLine prints the allowed values and the command currently exits 1, as it does for other usage errors. A script cannot tell that exit code from a run with violations, so check the format you pass.

## Paths

File paths in the JSON and SARIF reports are relative to the repository root, with `/` as the separator on every platform. The root is the nearest ancestor of the solution's folder that holds a `.git` file or folder. When no ancestor has one, the root is the solution's folder.

This differs from the text report, whose paths are relative to the solution's folder. A report uploaded from CI then names files the way the repository does, wherever the solution sits in it.

A file outside the root, such as a linked source file, keeps its absolute path in JSON. In SARIF it becomes an absolute `file://` uri without a `uriBaseId`.

## Encoding

Both formats are compact: one line, no indentation. They are UTF-8 without a byte order mark.

## JSON report

Ariadne's own format, versioned by `schema`. This page describes schema 1. Property names are camelCase.

```json
{
  "schema": 1,
  "tool": { "name": "ariadne", "version": "1.4.0" },
  "typesChecked": 2,
  "projects": [
    { "name": "Shop.Domain", "targetFrameworks": ["net10.0"] }
  ],
  "skippedProjects": [
    { "name": "Shop.Web", "reason": "Not a C# project." }
  ],
  "violations": [
    {
      "ruleId": "SHOP-LAYER-DOMAIN",
      "pack": "shop",
      "kind": "DependencyNotAllowed",
      "sourceType": "Shop.Domain.Order",
      "message": "Layer 'Domain' is not allowed to depend on 'Infrastructure': Shop.Domain.Order references Shop.Infrastructure.SqlStore 3 times",
      "because": "The domain holds business rules only.",
      "target": "Shop.Infrastructure.SqlStore",
      "location": { "path": "Shop.Domain/Order.cs", "line": 7, "column": 5 },
      "referenceCount": 3,
      "targetFrameworks": ["net10.0"]
    }
  ]
}
```

| Field | Meaning |
|-------|---------|
| `schema` | The schema version, `1`. |
| `tool` | `name` is `ariadne`; `version` is the Ariadne.NET version, without build metadata. |
| `typesChecked` | The number of types checked. |
| `projects` | Each checked project, with its `name` and the `targetFrameworks` it was checked for. |
| `skippedProjects` | Each project that was not checked, with its `name` and the `reason`. |
| `violations` | One entry per violation, in the order of the text report. |

A violation has these fields:

| Field | Meaning |
|-------|---------|
| `ruleId` | The rule id, such as a layer id, `ROLE-<Name>`, `CODE-001` or `CODE-002`. |
| `pack` | The name of the pack that holds the rule. |
| `kind` | The violation kind by name, such as `DependencyNotAllowed`, `ForbiddenDependency` or `LayerOverlap`. |
| `sourceType` | The type that breaks the rule. |
| `message` | The violation message. |
| `because` | The `because` of the layer or rule. |
| `target` | The type or package that is referenced. Omitted when the violation has no target. |
| `location` | `path`, `line` and `column` of the source type. Omitted when the type has no source file, as for a metadata type. |
| `referenceCount` | How many references the violation stands for. |
| `targetFrameworks` | The target frameworks the violation was found in. |

`because` and `pack` are always present. They are written as `""` when empty, never omitted. `target` and `location` are omitted when missing, never written as `null`.

A report without violations still has every top-level field, with `"violations": []`.

## SARIF report

The SARIF log follows version 2.1.0 and sets `$schema` to `https://json.schemastore.org/sarif-2.1.0.json`. Code scanning services read it without further setup.

The mapping:

| Ariadne | SARIF |
|---------|-------|
| The report | One `run`. |
| The tool | `tool.driver` with `name` `Ariadne.NET`, the `version` and an `informationUri`. |
| A distinct rule id and pack | One entry in `driver.rules`: `id` is the rule id, `shortDescription.text` the violation kind, `help.text` the `because`, `properties.pack` the pack. |
| A violation | One `result`. `ruleId` is the rule id, `ruleIndex` points into `driver.rules`, `level` is always `error`, `message.text` is the message. |
| `pack` and `targetFrameworks` | `result.properties`. |
| The location | `physicalLocation` with `artifactLocation` and a `region` holding `startLine` and `startColumn`. |
| No location | A `logicalLocations` entry instead: `name` is the source type, `kind` is `type`. |
| The root | `originalUriBaseIds` has `%SRCROOT%` set to the root as a `file://` uri. |

The same rule id from two packs gives two rules, because a pack can reuse an id with different `because` text. Each file `uri` is relative to the root, percent-encoded per segment, with `uriBaseId` `%SRCROOT%`. A file outside the root gets the absolute `file://` uri and no `uriBaseId`.

```json
{
  "$schema": "https://json.schemastore.org/sarif-2.1.0.json",
  "version": "2.1.0",
  "runs": [
    {
      "tool": {
        "driver": {
          "name": "Ariadne.NET",
          "version": "1.4.0",
          "informationUri": "https://marcelroozekrans.github.io/Ariadne.NET/",
          "rules": [
            {
              "id": "SHOP-LAYER-DOMAIN",
              "shortDescription": { "text": "DependencyNotAllowed" },
              "help": { "text": "The domain holds business rules only." },
              "properties": { "pack": "shop" }
            }
          ]
        }
      },
      "originalUriBaseIds": { "%SRCROOT%": { "uri": "file:///home/build/shop/" } },
      "results": [
        {
          "ruleId": "SHOP-LAYER-DOMAIN",
          "ruleIndex": 0,
          "level": "error",
          "message": { "text": "Layer 'Domain' is not allowed to depend on 'Infrastructure': Shop.Domain.Order references Shop.Infrastructure.SqlStore 3 times" },
          "locations": [
            {
              "physicalLocation": {
                "artifactLocation": { "uri": "Shop.Domain/Order.cs", "uriBaseId": "%SRCROOT%" },
                "region": { "startLine": 7, "startColumn": 5 }
              }
            }
          ],
          "properties": { "pack": "shop", "targetFrameworks": ["net10.0"] }
        }
      ]
    }
  ]
}
```

### Upload to GitHub code scanning

```yaml
- run: ariadne validate --format sarif --output ariadne.sarif
  continue-on-error: true
- uses: github/codeql-action/upload-sarif@v3
  with:
    sarif_file: ariadne.sarif
```

`continue-on-error` lets the upload run when the command exits 1. The job needs the `security-events: write` permission. Full CI examples are a separate page.

## From code

The writers are public in `Ariadne.NET.Reports`:

```csharp
var context = new ReportContext(ReportRoot.Find(solutionFolder), "1.4.0");
JsonReportWriter.Write(stream, report, context);
SarifReportWriter.Write(stream, report, context);
```

`ReportContext` carries the root that paths are made relative to and the tool version. `ReportFormat` (`Json`, `Sarif`) names the two formats. A test writes a report with `ArchitectureCheck`; see [Testing](13-testing.md#reports).
