namespace Fsgg

module Version =

    type Version = { Major: int; Minor: int; Patch: int }

    // Grammar extracted verbatim from the (formerly private) Registry SemVer engine
    // (Registry.fs:73-89); pre-release/build metadata are out of scope. Pure, total,
    // exception-free, BCL-only.
    let tryParse (text: string) : Version option =
        // NumberStyles.None + invariant culture: reject leading/trailing whitespace
        // and signs so " 2" and "+3" no longer slip through the default
        // Int32.TryParse (NumberStyles.Integer, current culture), which accepted
        // "1. 2.+3" as 1.2.3. This grammar gates provider minimumCliVersion coherence.
        let tryInt (s: string) =
            match System.Int32.TryParse(s, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture) with
            | true, v -> Some v
            | _ -> None

        match text.Split('.') with
        | [| a; b; c |] ->
            match tryInt a, tryInt b, tryInt c with
            | Some major, Some minor, Some patch when major >= 0 && minor >= 0 && patch >= 0 ->
                Some { Major = major; Minor = minor; Patch = patch }
            | _ -> None
        | _ -> None

    let private compareParsed (a: Version) (b: Version) =
        match compare a.Major b.Major with
        | 0 ->
            match compare a.Minor b.Minor with
            | 0 -> compare a.Patch b.Patch
            | c -> c
        | c -> c

    let compare (left: string) (right: string) : int option =
        match tryParse left, tryParse right with
        | Some l, Some r ->
            // Normalize the BCL compare to the documented sign contract (-1/0/1).
            Some(sign (compareParsed l r))
        | _ -> None
