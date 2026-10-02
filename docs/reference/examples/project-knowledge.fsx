// Run against a built Artifacts assembly; reads are supplied explicitly by the caller.
#r "../../../src/FS.GG.SDD.Artifacts/bin/Debug/net10.0/FS.GG.SDD.Artifacts.dll"
open FS.GG.SDD.Artifacts
let query: Knowledge.Query = { Text = "experiment"; Kind = None; Status = None; Scope = None }
let find snapshots = Knowledge.load snapshots |> Result.map (Knowledge.search query)
