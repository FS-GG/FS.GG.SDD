namespace FS.GG.SDD.Artifacts

module FsharpIdentifier =

    type DerivationError = Unrepresentable of name: string

    // F# keywords (and the hard reserved words `const`/`mod`, which the compiler rejects
    // as identifiers — FS0010, unlike the merely-warned reserved-for-future words). A
    // derived segment equal to one of these is suffixed with `_` so it stays usable in a
    // `namespace`/`module` position. Kept as an explicit, language-level literal
    // (Principle IV — no clever machinery).
    let private keywords =
        set
            [ "abstract"
              "and"
              "as"
              "assert"
              "base"
              "begin"
              "class"
              "const"
              "default"
              "delegate"
              "do"
              "done"
              "downcast"
              "downto"
              "elif"
              "else"
              "end"
              "exception"
              "extern"
              "false"
              "finally"
              "fixed"
              "for"
              "fun"
              "function"
              "global"
              "if"
              "in"
              "inherit"
              "inline"
              "interface"
              "internal"
              "lazy"
              "let"
              "match"
              "member"
              "mod"
              "module"
              "mutable"
              "namespace"
              "new"
              "not"
              "null"
              "of"
              "open"
              "or"
              "override"
              "private"
              "public"
              "rec"
              "return"
              "sig"
              "static"
              "struct"
              "then"
              "to"
              "true"
              "try"
              "type"
              "upcast"
              "use"
              "val"
              "void"
              "when"
              "while"
              "with"
              "yield" ]

    // Keep only characters valid in an F# identifier body: Unicode letters, digits,
    // and underscore. Ordinal / culture-invariant (Char.IsLetterOrDigit is decided by
    // Unicode category, not culture), so the result is deterministic across platforms.
    let private isIdentifierChar (c: char) =
        System.Char.IsLetterOrDigit c || c = '_'

    let private deriveSegment (segment: string) =
        let filtered = segment |> String.filter isIdentifierChar

        // A usable segment needs at least one letter or digit. An empty or all-underscore
        // filter is collapsed (contributes no segment) — this also rejects a lone `_`,
        // which F# treats as the wildcard, not a valid namespace/module identifier.
        if not (filtered |> String.exists System.Char.IsLetterOrDigit) then
            None
        else
            // First char may not be a digit; keywords are made safe with a suffix.
            let guarded =
                if System.Char.IsDigit filtered.[0] then
                    "_" + filtered
                else
                    filtered

            Some(
                if Set.contains guarded keywords then
                    guarded + "_"
                else
                    guarded
            )

    let deriveNamespace (name: string) : Result<string, DerivationError> =
        let derived = name.Split('.') |> Array.choose deriveSegment

        if derived.Length = 0 then
            Error(Unrepresentable name)
        else
            Ok(String.concat "." derived)
