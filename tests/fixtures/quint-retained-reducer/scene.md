# Retained scene reducer

A bounded retained scene keeps a monotonic revision and one selected identity. This consumer-defined,
tooling-only fixture proves installed profile-2 authoring; Rendering owns the later canonical reducer model.

```quint scene.qnt +=
module RetainedSceneReducer {
  type State = { revision: int, selected: str }
  pure val initialState = { revision: 0, selected: "" }

  var revision: int
  var selected: str

  action init = all {
    revision' = initialState.revision,
    selected' = initialState.selected,
  }

  action choose(id: str): bool = all {
    revision' = revision + 1,
    selected' = id,
  }
}
```
