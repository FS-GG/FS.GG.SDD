namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.Text

[<Struct>]
type SpecificationId = private SpecificationId of string

[<RequireQualifiedAccess>]
module SpecificationId =
    let create (value: string) =
        let valid character =
            (character >= 'A' && character <= 'Z')
            || (character >= '0' && character <= '9')
            || character = '-'

        if String.IsNullOrWhiteSpace value || value.Length < 5 then
            Error "Specification identifiers require at least five uppercase ASCII characters."
        elif value |> Seq.exists (valid >> not) then
            Error "Specification identifiers use uppercase ASCII letters, digits, and hyphens."
        elif value.StartsWith("-") || value.EndsWith("-") || value.Contains("--") then
            Error "Specification identifiers cannot begin/end with or repeat a hyphen."
        else Ok(SpecificationId value)

    let value (SpecificationId value) = value

type SourceLocation = { Line: int; Column: int }
type SpecificationProvenance =
    { Agent: string; Session: string; SourcePath: string; SourceRevision: string; AuthoredAtUtc: string }
type EvidenceObligation = { Id: SpecificationId; Kind: string; Description: string }
type EvidenceReceipt = { ObligationId: SpecificationId; Kind: string; EvidenceRef: string }
type SpecificationDiagnostic =
    { Code: string; Path: string; Message: string; Location: SourceLocation option }
type SpecificationModel<'extension> =
    { Identity: SpecificationId
      SchemaVersion: int
      Provenance: SpecificationProvenance
      Intent: string
      EvidenceObligations: EvidenceObligation list
      Extension: 'extension }

/// Portable subset of the extension contract. JSON codecs and generated-view IO remain net10.0-only.
type ExtensionContract<'extension> =
    { Kind: string
      SchemaVersion: int
      Validate: EvidenceObligation list -> 'extension -> SpecificationDiagnostic list
      EncodeCanonical: 'extension -> byte array
      ProjectMarkdown: 'extension -> string list }

type CompiledSpecification<'extension> =
    { Model: SpecificationModel<'extension>; NormalizedBytes: byte array; Fingerprint: string }
type SemanticChange =
    { Path: string; Summary: string; BeforeFingerprint: string; AfterFingerprint: string }
type SemanticDiff = Equivalent | Changed of SemanticChange list
type EvidenceValidation = { Satisfied: SpecificationId list; Diagnostics: SpecificationDiagnostic list }
type SpecificationProjection =
    { Markdown: string; Json: string; SourceFingerprint: string; GeneratedFingerprint: string }
type ProjectionObservation = Missing | Unreadable of detail: string | Content of text: string
type MigrationReason = UnresolvedReference | UnknownSemanticHeading | UnsupportedSchemaVersion | MalformedConstruct
type MigrationFinding = { Code: string; Reason: MigrationReason; Message: string; Location: SourceLocation }
type MigrationOutcome<'model> = Migrated of 'model | Ambiguous of MigrationFinding list | Unsupported of MigrationFinding list

module private Portable =
    let diagnostic code path message : SpecificationDiagnostic =
        { Code = code; Path = path; Message = message; Location = None }

    let sort (diagnostics: SpecificationDiagnostic list) =
        diagnostics |> List.distinct |> List.sortBy (fun item -> item.Path, item.Code, item.Message)

    let int32 value =
        [| byte value; byte (value >>> 8); byte (value >>> 16); byte (value >>> 24) |]

    let frameBytes (bytes: byte array) = Array.concat [ int32 bytes.Length; bytes ]
    let frameText (value: string) = value |> Encoding.UTF8.GetBytes |> frameBytes

    let evidenceBytes obligations =
        let rows =
            obligations
            |> List.sortBy (fun item -> SpecificationId.value item.Id)
            |> List.collect (fun item ->
                [ frameText (SpecificationId.value item.Id); frameText item.Kind; frameText item.Description ])
        Array.concat (int32 obligations.Length :: rows)

    let normalizedBytes (contract: ExtensionContract<'extension>) (model: SpecificationModel<'extension>) =
        Array.concat
            [ frameText "fsgg-typed-specification/v1"
              frameText (SpecificationId.value model.Identity)
              int32 model.SchemaVersion
              frameText model.Provenance.SourcePath
              frameText model.Provenance.SourceRevision
              evidenceBytes model.EvidenceObligations
              frameText contract.Kind
              int32 contract.SchemaVersion
              contract.EncodeCanonical model.Extension |> frameBytes ]

    let validate (contract: ExtensionContract<'extension>) (model: SpecificationModel<'extension>) =
        let blank code path name value =
            if String.IsNullOrWhiteSpace value then [ diagnostic code path (name + " is required.") ] else []
        let lowercaseHex (value: string) =
            not (String.IsNullOrWhiteSpace value)
            && (value.Length = 40 || value.Length = 64)
            && (value |> Seq.forall (fun c -> (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
        [ if model.SchemaVersion <> 1 then yield diagnostic "SPEC-SCHEMA-UNSUPPORTED" "/schemaVersion" "Only specification schema version 1 is supported."
          yield! blank "SPEC-CONTRACT-KIND" "/extensionKind" "Extension kind" contract.Kind
          if contract.SchemaVersion <= 0 then yield diagnostic "SPEC-CONTRACT-SCHEMA" "/extensionSchemaVersion" "Extension schema version must be positive."
          yield! blank "SPEC-PROVENANCE-AGENT" "/provenance/agent" "Provenance agent" model.Provenance.Agent
          yield! blank "SPEC-PROVENANCE-SESSION" "/provenance/session" "Provenance session" model.Provenance.Session
          yield! blank "SPEC-PROVENANCE-SOURCE" "/provenance/sourcePath" "Provenance source path" model.Provenance.SourcePath
          if not (lowercaseHex model.Provenance.SourceRevision) then yield diagnostic "SPEC-PROVENANCE-REVISION" "/provenance/sourceRevision" "Source revision must be a 40- or 64-character lowercase hexadecimal digest."
          match DateTimeOffset.TryParse model.Provenance.AuthoredAtUtc with
          | true, _ -> ()
          | _ -> yield diagnostic "SPEC-PROVENANCE-TIME" "/provenance/authoredAtUtc" "Authored time must be an ISO-8601 instant."
          yield! blank "SPEC-INTENT-REQUIRED" "/intent" "Authoring intent" model.Intent
          for index, obligation in model.EvidenceObligations |> List.indexed do
              yield! blank "SPEC-EVIDENCE-KIND-REQUIRED" ($"/evidenceObligations/%d{index}/kind") "Evidence kind" obligation.Kind
              yield! blank "SPEC-EVIDENCE-DESCRIPTION-REQUIRED" ($"/evidenceObligations/%d{index}/description") "Evidence description" obligation.Description
          for duplicate, count in model.EvidenceObligations |> List.countBy _.Id do
              if count > 1 then yield diagnostic "SPEC-EVIDENCE-ID-DUPLICATE" "/evidenceObligations" ($"Evidence obligation '%s{SpecificationId.value duplicate}' is declared more than once.")
          yield! contract.Validate model.EvidenceObligations model.Extension ] |> sort

    let constants =
        [| 0x428a2f98u;0x71374491u;0xb5c0fbcfu;0xe9b5dba5u;0x3956c25bu;0x59f111f1u;0x923f82a4u;0xab1c5ed5u
           0xd807aa98u;0x12835b01u;0x243185beu;0x550c7dc3u;0x72be5d74u;0x80deb1feu;0x9bdc06a7u;0xc19bf174u
           0xe49b69c1u;0xefbe4786u;0x0fc19dc6u;0x240ca1ccu;0x2de92c6fu;0x4a7484aau;0x5cb0a9dcu;0x76f988dau
           0x983e5152u;0xa831c66du;0xb00327c8u;0xbf597fc7u;0xc6e00bf3u;0xd5a79147u;0x06ca6351u;0x14292967u
           0x27b70a85u;0x2e1b2138u;0x4d2c6dfcu;0x53380d13u;0x650a7354u;0x766a0abbu;0x81c2c92eu;0x92722c85u
           0xa2bfe8a1u;0xa81a664bu;0xc24b8b70u;0xc76c51a3u;0xd192e819u;0xd6990624u;0xf40e3585u;0x106aa070u
           0x19a4c116u;0x1e376c08u;0x2748774cu;0x34b0bcb5u;0x391c0cb3u;0x4ed8aa4au;0x5b9cca4fu;0x682e6ff3u
           0x748f82eeu;0x78a5636fu;0x84c87814u;0x8cc70208u;0x90befffau;0xa4506cebu;0xbef9a3f7u;0xc67178f2u |]
    let rotate n v = (v >>> n) ||| (v <<< (32-n))
    let bigEndian v = [|byte(v>>>24);byte(v>>>16);byte(v>>>8);byte v|]
    let sha256 (bytes: byte array) =
        let length = uint32 bytes.Length
        let zeros = (56 - ((bytes.Length + 1) % 64) + 64) % 64
        let padded = Array.concat [ bytes; [|0x80uy|]; Array.zeroCreate zeros; bigEndian (length>>>29); bigEndian (length<<<3) ]
        let hash = [|0x6a09e667u;0xbb67ae85u;0x3c6ef372u;0xa54ff53au;0x510e527fu;0x9b05688cu;0x1f83d9abu;0x5be0cd19u|]
        for start in 0 .. 64 .. padded.Length-64 do
            let w = Array.zeroCreate<uint32> 64
            for i in 0..15 do let o=start+i*4 in w[i] <- (uint32 padded[o]<<<24)|||(uint32 padded[o+1]<<<16)|||(uint32 padded[o+2]<<<8)|||uint32 padded[o+3]
            for i in 16..63 do
                let a=w[i-15] in let b=w[i-2]
                w[i] <- w[i-16] + (rotate 7 a ^^^ rotate 18 a ^^^ (a>>>3)) + w[i-7] + (rotate 17 b ^^^ rotate 19 b ^^^ (b>>>10))
            let mutable a=hash[0] in let mutable b=hash[1] in let mutable c=hash[2] in let mutable d=hash[3]
            let mutable e=hash[4] in let mutable f=hash[5] in let mutable g=hash[6] in let mutable h=hash[7]
            for i in 0..63 do
                let t1=h+(rotate 6 e ^^^ rotate 11 e ^^^ rotate 25 e)+((e&&&f)^^^((~~~e)&&&g))+constants[i]+w[i]
                let t2=(rotate 2 a ^^^ rotate 13 a ^^^ rotate 22 a)+((a&&&b)^^^(a&&&c)^^^(b&&&c))
                h<-g;g<-f;f<-e;e<-d+t1;d<-c;c<-b;b<-a;a<-t1+t2
            hash[0]<-hash[0]+a;hash[1]<-hash[1]+b;hash[2]<-hash[2]+c;hash[3]<-hash[3]+d
            hash[4]<-hash[4]+e;hash[5]<-hash[5]+f;hash[6]<-hash[6]+g;hash[7]<-hash[7]+h
        hash |> Array.collect bigEndian

    let digest bytes = sha256 bytes |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

[<RequireQualifiedAccess>]
module SpecificationCompiler =
    let validate contract model = Portable.validate contract model
    let normalize contract model = match validate contract model with | [] -> Ok(Portable.normalizedBytes contract model) | errors -> Error errors
    let fingerprint contract model = normalize contract model |> Result.map Portable.digest
    let compile contract model =
        match normalize contract model with
        | Error errors -> Error errors
        | Ok bytes -> Ok { Model=model; NormalizedBytes=bytes; Fingerprint=Portable.digest bytes }
    let semanticDiff contract before after =
        let diagnostics = Portable.sort (validate contract before @ validate contract after)
        if not diagnostics.IsEmpty then Error diagnostics else
        let digest = Portable.digest
        let text value = Portable.frameText value |> digest
        let integer value = Portable.int32 value |> digest
        let evidence model = Portable.evidenceBytes model.EvidenceObligations |> digest
        let extension model = contract.EncodeCanonical model.Extension |> digest
        let changes =
            [ if before.Identity<>after.Identity then yield {Path="/identity";Summary="Specification identity changed.";BeforeFingerprint=text(SpecificationId.value before.Identity);AfterFingerprint=text(SpecificationId.value after.Identity)}
              if before.SchemaVersion<>after.SchemaVersion then yield {Path="/schemaVersion";Summary="Specification schema version changed.";BeforeFingerprint=integer before.SchemaVersion;AfterFingerprint=integer after.SchemaVersion}
              if before.Provenance.SourcePath<>after.Provenance.SourcePath then yield {Path="/provenance/sourcePath";Summary="Authoritative source path changed.";BeforeFingerprint=text before.Provenance.SourcePath;AfterFingerprint=text after.Provenance.SourcePath}
              if before.Provenance.SourceRevision<>after.Provenance.SourceRevision then yield {Path="/provenance/sourceRevision";Summary="Authoritative source revision changed.";BeforeFingerprint=text before.Provenance.SourceRevision;AfterFingerprint=text after.Provenance.SourceRevision}
              if evidence before<>evidence after then yield {Path="/evidenceObligations";Summary="Evidence obligations changed.";BeforeFingerprint=evidence before;AfterFingerprint=evidence after}
              if extension before<>extension after then yield {Path="/extension";Summary="Typed specification extension changed.";BeforeFingerprint=extension before;AfterFingerprint=extension after} ]
        Ok(if changes.IsEmpty then Equivalent else Changed changes)

[<RequireQualifiedAccess>]
module SpecificationEvidence =
    let validate obligations receipts =
        let satisfied =
            obligations
            |> List.choose (fun obligation -> receipts |> List.tryFind (fun receipt -> receipt.ObligationId=obligation.Id && receipt.Kind=obligation.Kind) |> Option.map (fun _ -> obligation.Id))
        { Satisfied=satisfied; Diagnostics=[] }
