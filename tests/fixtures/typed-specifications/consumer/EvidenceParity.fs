open FS.GG.SDD.Artifacts.TypedSpecifications

let identifier value =
    match SpecificationId.create value with
    | Ok id -> id
    | Error message -> failwith message

let evidenceId = identifier "EVIDENCE-001"

let obligations =
    [ { Id = evidenceId
        Kind = "test"
        Description = "Must have a nonblank evidence reference." } ]

let receipts =
    [ { ObligationId = evidenceId
        Kind = "wrong-kind"
        EvidenceRef = "" }
      { ObligationId = evidenceId
        Kind = "wrong-kind"
        EvidenceRef = "" } ]

let result = SpecificationEvidence.validate obligations receipts

printfn "satisfied=%d" result.Satisfied.Length

result.Diagnostics
|> List.map _.Code
|> String.concat ","
|> printfn "diagnostics=%s"
