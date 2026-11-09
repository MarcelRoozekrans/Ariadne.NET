---
sidebar_position: 3.6
---

# Editor Support

Ariadne.NET publishes a JSON Schema for schema 2 packs. An editor that reads it underlines unknown keys, wrong types and a missing `schema`, completes keys and shows the description of each one while you write a pack, long before you run `ariadne validate`.

The schema is at:

```text
https://marcelroozekrans.github.io/Ariadne.NET/schema/pack.schema.json
```

## Use it in a pack

Add this comment as the first line of the pack. VS Code with the YAML extension, Rider and other editors that use the YAML language server read it:

```yaml
# yaml-language-server: $schema=https://marcelroozekrans.github.io/Ariadne.NET/schema/pack.schema.json
schema: 2
name: shop
```

## Use it for every pack in a folder

In VS Code, map the schema to your pack file in the workspace settings. Do not map `.architecture/*.yaml`: the imports file `.architecture/imports.yaml` lives in the same folder and is not a pack.

```json
{
  "yaml.schemas": {
    "https://marcelroozekrans.github.io/Ariadne.NET/schema/pack.schema.json": ".architecture/rules.yaml"
  }
}
```

## What the schema checks

The schema checks the shape of a pack, like the loader does: every mapping has a closed set of keys, required keys are present, lists that need an entry are not empty, values have the right type, a rule has an `id`, a `because` and exactly one rule type, `layers` is not mixed with other selector keys, and `schema` is `2`. Plain numbers and booleans count as text where the loader reads text, so `version: 1.0` is fine. It follows the [rule schema](03-rule-schema.md) and [rule types](rule-types.md) pages.

## What the schema cannot check

Some errors depend on other parts of the pack, or on the packs it extends, and JSON Schema cannot express them. `ariadne validate` and the loader still report these:

- A layer in `allowedDependencies` or a rule target that no layer defines.
- A layer or rule `id` that appears twice in the merged pack.
- A child pack that changes what it may not, or adds exceptions to a locked rule.
- A pack that needs `extends` to supply the layers its roles name.
- A rule entry with only `id` and `exceptions`, or a layer entry that only retargets, in a pack without `extends`.
- A malformed glob or member pattern, such as a member pattern without a dot.
- `schema: 2.0` or `schema: 02`: an editor reads these as the number 2, and the loader accepts only `2`.

A pack the schema rejects does not load either, except in the corner cases above, so an error in the editor is a real error. A pack the schema accepts can still fail to load or resolve. Tests in the repository keep the two in agreement on the cases they list: every built-in pack validates against the schema, each invalid pack in a test corpus is rejected by both, and the cases above are pinned as known gaps.
