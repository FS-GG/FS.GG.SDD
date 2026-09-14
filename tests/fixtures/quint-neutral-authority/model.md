# Neutral cooperative turn model

This bounded tooling fixture describes a cooperative turn counter and exports its authority facts.

```quint cooperative.qnt +=
module CooperativeTurn {
  type State = { turn: int, ready: bool }
  pure val initialState = { turn: 0, ready: false }

  pure val authorityFacts = [
    { id: "STATE-Turn", kind: "state", fromId: "", toId: "", verificationKind: "", subjectIds: Set(), boundIds: Set(), minimum: 0, maximum: 0, subjectId: "", category: "", detail: "Shared cooperative turn.", surface: "", requirement: "" },
    { id: "REL-Turn-Requires-Bound", kind: "requires", fromId: "STATE-Turn", toId: "BOUND-Turn", verificationKind: "", subjectIds: Set(), boundIds: Set(), minimum: 0, maximum: 0, subjectId: "", category: "", detail: "", surface: "", requirement: "" },
    { id: "VERIFY-Turn", kind: "verification", fromId: "", toId: "", verificationKind: "model-check", subjectIds: Set("STATE-Turn"), boundIds: Set("BOUND-Turn"), minimum: 0, maximum: 0, subjectId: "", category: "", detail: "", surface: "", requirement: "" },
    { id: "BOUND-Turn", kind: "bound", fromId: "", toId: "", verificationKind: "", subjectIds: Set(), boundIds: Set(), minimum: 0, maximum: 4, subjectId: "", category: "", detail: "", surface: "", requirement: "" },
    { id: "IMPACT-Turn", kind: "impact", fromId: "", toId: "", verificationKind: "", subjectIds: Set(), boundIds: Set(), minimum: 0, maximum: 0, subjectId: "STATE-Turn", category: "runtime", detail: "Advancing changes the shared turn.", surface: "", requirement: "" },
    { id: "COMPAT-Turn", kind: "compatibility", fromId: "", toId: "", verificationKind: "", subjectIds: Set(), boundIds: Set(), minimum: 0, maximum: 0, subjectId: "", category: "", detail: "The turn remains a signed integer.", surface: "player", requirement: "turn-int64" },
  ]

  var turn: int
  var ready: bool

  action init = all {
    turn' = initialState.turn,
    ready' = initialState.ready,
  }

  action advance = all {
    turn' = turn + 1,
    ready' = true,
  }

}
```
