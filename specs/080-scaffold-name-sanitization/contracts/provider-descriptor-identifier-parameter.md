# Contract: `ProviderDescriptor.IdentifierParameter` (additive, v1)

**Package**: `FS.GG.Contracts` (F# namespace `Fsgg`, module `Provider`) — org-shared descriptor.

**Change class**: additive superset. The existing eleven fields are byte-for-byte unchanged; one
optional field is added. No consumer breaks: a descriptor that omits it parses to `None`.

## Type delta (`Provider.fs` / `Provider.fsi`)

```fsharp
type ProviderDescriptor =
    { Name: string
      ContractVersion: string
      TemplateId: string
      Source: string
      Parameters: ProviderParameterSpec list
      Build: DeclaredCommand option
      Test: DeclaredCommand option
      Run: DeclaredCommand option
      Verify: DeclaredCommand option
      NameParameter: string
      /// NEW. The forwarded parameter key that receives the SDD-derived valid-F#
      /// identifier (the derivation *sink*). `None` ⇒ scaffold performs no derivation
      /// and forwards parameters exactly as before (backward compatible). Provider-
      /// declared and value-agnostic: generic SDD reads the key, never a fixed value.
      IdentifierParameter: string option
      MinimumCliVersion: string option }
```

`.fsi` gains the field with the same doc comment. `defaultNameParameter` / `resolveNameParameter`
unchanged. No new helper required (a `None` check at the call site suffices).

## Registry surface (`providers.yml`)

```yaml
providers:
  - name: rendering
    contractVersion: "1.x.y"
    templateId: <provider-owned>
    source: <provider-owned>
    nameParameter: productName          # source: carries the raw product name
    identifierParameter: rootNamespace  # NEW sink: receives the derived F# identifier
    parameters:
      - { key: productName, required: true }
      # rootNamespace need not be declared as a parameter; SDD injects it.
```

Parsing (`FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs`): read optional
`identifierParameter:` scalar → `IdentifierParameter`; blank/whitespace ⇒ `None`. Does **not**
affect entry-drop (still gated only on `name`/`contractVersion`/`templateId`/`source`).

## Compatibility

- SDD ≥ this feature + provider that declares `identifierParameter` ⇒ derived identifier
  forwarded and consumed → scaffold compiles.
- SDD ≥ this feature + provider that omits it ⇒ no derivation → identical to today.
- SDD < this feature + provider that declares it ⇒ field ignored (older SDD never reads it) →
  identical to today (provider's template must still compile with the raw name, i.e. the bug —
  hence the coordinated adoption in D8).

## Cross-repo

Reference-provider adoption (FS.GG.Rendering: template symbol in identifier contexts + descriptor
declaration) is a `cross-repo:request` and a **versioned additive** contract change recorded in
`FS-GG/.github` `registry/dependencies.yml` + `docs/registry/compatibility.md`, sequenced on the
Coordination board (research D8).
