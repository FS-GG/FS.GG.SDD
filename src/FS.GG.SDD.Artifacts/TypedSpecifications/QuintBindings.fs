namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.Globalization
open System.Security.Cryptography
open System.Text

type QuintBindingDiagnostic =
    { Code: string
      Path: string
      Message: string }

type QuintGeneratedBindings =
    { CanonicalJson: string
      ContractFingerprint: string
      Identifiers: string list
      FSharpSource: string
      FableSource: string }

module private BindingInternal =
    let diagnostic code path message : QuintBindingDiagnostic =
        { Code = code
          Path = path
          Message = message }

    let sortDiagnostics diagnostics =
        diagnostics
        |> List.distinct
        |> List.sortBy (fun item -> item.Path, item.Code, item.Message)

    let escapeString (value: string) =
        let builder = StringBuilder(value.Length + 2)
        builder.Append('"') |> ignore

        for character in value do
            match character with
            | '"' -> builder.Append("\\\"") |> ignore
            | '\\' -> builder.Append("\\\\") |> ignore
            | '\b' -> builder.Append("\\b") |> ignore
            | '\f' -> builder.Append("\\f") |> ignore
            | '\n' -> builder.Append("\\n") |> ignore
            | '\r' -> builder.Append("\\r") |> ignore
            | '\t' -> builder.Append("\\t") |> ignore
            | value when int value < 0x20 ->
                builder.Append("\\u").Append((int value).ToString("x4", CultureInfo.InvariantCulture))
                |> ignore
            | value -> builder.Append(value) |> ignore

        builder.Append('"').ToString()

    let reserved =
        set
            [ "Abstract"
              "And"
              "As"
              "Assert"
              "Base"
              "Begin"
              "Class"
              "Default"
              "Delegate"
              "Do"
              "Done"
              "Downcast"
              "Downto"
              "Elif"
              "Else"
              "End"
              "Exception"
              "Extern"
              "False"
              "Finally"
              "Fixed"
              "For"
              "Fun"
              "Function"
              "Global"
              "If"
              "In"
              "Inherit"
              "Inline"
              "Interface"
              "Internal"
              "Lazy"
              "Let"
              "Match"
              "Member"
              "Module"
              "Mutable"
              "Namespace"
              "New"
              "Not"
              "Null"
              "Of"
              "Open"
              "Or"
              "Override"
              "Private"
              "Public"
              "Rec"
              "Return"
              "Sig"
              "Static"
              "Struct"
              "Then"
              "To"
              "True"
              "Try"
              "Type"
              "Upcast"
              "Use"
              "Val"
              "Void"
              "When"
              "While"
              "With"
              "Yield" ]

    let identifier (wireName: string) =
        let words =
            wireName
            |> Seq.fold
                (fun (parts, current: StringBuilder) character ->
                    if
                        (character >= 'a' && character <= 'z')
                        || (character >= 'A' && character <= 'Z')
                        || (character >= '0' && character <= '9')
                    then
                        parts, current.Append(character)
                    elif current.Length = 0 then
                        parts, current
                    else
                        current.ToString() :: parts, StringBuilder())
                ([], StringBuilder())
            |> fun (parts, current) ->
                if current.Length = 0 then
                    List.rev parts
                else
                    List.rev (current.ToString() :: parts)
            |> List.map (fun word ->
                if word.Length = 0 then
                    ""
                elif word |> Seq.forall (fun character -> not (character >= 'a' && character <= 'z')) then
                    word.Substring(0, 1).ToUpperInvariant() + word.Substring(1).ToLowerInvariant()
                else
                    word.Substring(0, 1).ToUpperInvariant() + word.Substring(1))

        let candidate = String.concat "" words

        let candidate =
            if String.IsNullOrEmpty candidate then
                ""
            elif candidate[0] >= '0' && candidate[0] <= '9' then
                "_" + candidate
            else
                candidate

        if reserved.Contains candidate then
            "_" + candidate
        else
            candidate

    let kindText =
        function
        | QuintCatalogueKind.Requirement -> "requirement"
        | QuintCatalogueKind.StateVariable -> "state-variable"
        | QuintCatalogueKind.Action -> "action"
        | QuintCatalogueKind.Invariant -> "invariant"
        | QuintCatalogueKind.TemporalProperty -> "temporal-property"
        | QuintCatalogueKind.ReachabilityProperty -> "reachability-property"
        | QuintCatalogueKind.Evidence -> "evidence"
        | QuintCatalogueKind.Implementation -> "implementation"
        | QuintCatalogueKind.ExternalSubject -> "external-subject"

    let validate moduleName contract =
        let contractDiagnostics =
            QuintContract.validate contract
            |> List.map (fun item -> diagnostic item.Code item.Path item.Message)

        let rows =
            contract.Catalogue
            |> List.sortWith (fun left right ->
                let byId = StringComparer.Ordinal.Compare(left.Id, right.Id)

                if byId <> 0 then
                    byId
                else
                    StringComparer.Ordinal.Compare(kindText left.Kind, kindText right.Kind))

        let bindingDiagnostics =
            [ if String.IsNullOrWhiteSpace moduleName || identifier moduleName <> moduleName then
                  diagnostic
                      "QBD-MODULE-NAME"
                      "$.moduleName"
                      "Module name must already be one generated PascalCase identifier."

              for id, items in rows |> List.groupBy (fun item -> item.Id) |> List.sortBy fst do
                  if items.Length > 1 then
                      diagnostic
                          "QBD-CATALOGUE-ID-DUPLICATE"
                          "$.catalogue"
                          $"Catalogue identity '%s{id}' cannot generate more than one binding."

              for generated, items in rows |> List.groupBy (fun item -> identifier item.Id) |> List.sortBy fst do
                  if String.IsNullOrEmpty generated then
                      let ids = items |> List.map (fun item -> item.Id) |> List.sort |> String.concat ", "

                      diagnostic
                          "QBD-IDENTIFIER-EMPTY"
                          "$.catalogue"
                          $"Catalogue identities [%s{ids}] do not contain an ASCII letter or digit."
                  elif items.Length > 1 then
                      let ids = items |> List.map (fun item -> item.Id) |> List.sort |> String.concat ", "

                      diagnostic
                          "QBD-IDENTIFIER-COLLISION"
                          "$.catalogue"
                          $"Catalogue identities [%s{ids}] collide as generated identifier '%s{generated}'." ]

        sortDiagnostics (contractDiagnostics @ bindingDiagnostics)

    let fingerprint (canonicalJson: string) =
        let bytes: byte array = Encoding.UTF8.GetBytes canonicalJson
        let digest: byte array = SHA256.HashData bytes
        Convert.ToHexString(digest).ToLowerInvariant()

    let source (moduleName: string) (fingerprint: string) (contract: QuintCompiledContract) (canonicalJson: string) =
        let rows =
            contract.Catalogue
            |> List.sortWith (fun left right -> StringComparer.Ordinal.Compare(left.Id, right.Id))

        let builder = StringBuilder()

        builder.AppendLine("// <auto-generated />").Append("module ").AppendLine(moduleName).AppendLine()
        |> ignore

        builder.Append("[<Literal>]\nlet Schema = ").AppendLine(escapeString contract.Schema)
        |> ignore

        builder.Append("[<Literal>]\nlet Profile = ").AppendLine(escapeString contract.Profile)
        |> ignore

        builder.Append("[<Literal>]\nlet Specification = ").AppendLine(escapeString contract.Specification)
        |> ignore

        builder.Append("[<Literal>]\nlet ContractFingerprint = ").AppendLine(escapeString fingerprint)
        |> ignore

        builder.Append("[<Literal>]\nlet CanonicalContractJson = ").AppendLine(escapeString canonicalJson)
        |> ignore

        builder
            .AppendLine()
            .AppendLine("type CatalogueEntry =")
            .AppendLine("    { Id: string")
            .AppendLine("      Kind: string }")
        |> ignore

        builder.AppendLine().AppendLine("module Ids =") |> ignore

        for row in rows do
            builder
                .Append("    [<Literal>]\n    let ")
                .Append(identifier row.Id)
                .Append(" = ")
                .AppendLine(escapeString row.Id)
            |> ignore

        builder.AppendLine().AppendLine("let Catalogue : CatalogueEntry list =")
        |> ignore

        for index, row in rows |> List.indexed do
            let prefix = if index = 0 then "    [ " else "      "
            let suffix = if index = rows.Length - 1 then " ]" else ""

            builder
                .Append(prefix)
                .Append("{ Id = ")
                .Append(escapeString row.Id)
                .Append("; Kind = ")
                .Append(escapeString (kindText row.Kind))
                .Append(" }")
                .AppendLine(suffix)
            |> ignore

        builder.ToString().Replace("\r\n", "\n")

[<RequireQualifiedAccess>]
module QuintBindings =
    let generate moduleName contract =
        match BindingInternal.validate moduleName contract with
        | diagnostics when not diagnostics.IsEmpty -> Error diagnostics
        | _ ->
            match QuintContract.serializeCanonical contract with
            | Error findings ->
                findings
                |> List.map (fun item -> BindingInternal.diagnostic item.Code item.Path item.Message)
                |> BindingInternal.sortDiagnostics
                |> Error
            | Ok canonicalJson ->
                let fingerprint = BindingInternal.fingerprint canonicalJson
                let source = BindingInternal.source moduleName fingerprint contract canonicalJson

                let identifiers =
                    contract.Catalogue
                    |> List.sortWith (fun left right -> StringComparer.Ordinal.Compare(left.Id, right.Id))
                    |> List.map (fun row -> BindingInternal.identifier row.Id)

                Ok
                    { CanonicalJson = canonicalJson
                      ContractFingerprint = fingerprint
                      Identifiers = identifiers
                      FSharpSource = source
                      FableSource = source }
