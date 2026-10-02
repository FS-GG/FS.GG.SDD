namespace FS.GG.SDD.Cli

/// Read-only knowledge CLI; filesystem access is confined to the effect interpreter.
module Knowledge =
    type Request = {
        Root: string; Verb: string; Id: string
        Query: FS.GG.SDD.Artifacts.Knowledge.Query; TextOutput: bool
    }
    type Model = {
        Request: Request
        Result: Result<FS.GG.SDD.Artifacts.Knowledge.Store, FS.GG.SDD.Artifacts.Knowledge.Diagnostic list> option
    }
    type Msg = Loaded of Result<FS.GG.SDD.Artifacts.Knowledge.Snapshot list, FS.GG.SDD.Artifacts.Knowledge.Diagnostic list>
    type Effect = ReadStore of string
    val init: request: Request -> Model * Effect list
    val update: msg: Msg -> model: Model -> Model * Effect list
    val run: args: string list -> int
