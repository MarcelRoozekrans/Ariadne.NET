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

In VS Code, map the schema to the pack files in the workspace settings:

```json
{
  "yaml.schemas": {
    "https://marcelroozekrans.github.io/Ariadne.NET/schema/pack.schema.json": ".architecture/*.yaml"
  }
}
```

## What the schema checks

The schema checks the shape of a pack, like the loader does: every mapping has a closed set of keys, values have the right type, a rule has an `id`, a `because` and exactly one rule type, and `layers` is not mixed with other selector keys. It follows the [rule schema](03-rule-schema.md) and [rule types](rule-types.md) pages.

## What the schema cannot check

Some errors depend on other parts of the pack, or on the packs it extends, and JSON Schema cannot express them. `ariadne validate` and the loader still report these:

- A layer in `allowedDependencies` or a rule target that no layer defines.
- A layer or rule `id` that appears twice in the merged pack.
- A child pack that changes what it may not, or adds exceptions to a locked rule.
- A pack that needs `extends` to supply the layers its roles name.

A pack the schema accepts can therefore still fail to resolve, but a pack the schema rejects for its shape does not load either. Tests in the repository keep the two in agreement: every built-in pack validates against the schema, and each invalid pack in a test corpus is rejected by both.
