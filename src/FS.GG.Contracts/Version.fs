namespace Fsgg

module Version =

    type Version = { Major: int; Minor: int; Patch: int }

    // Grammar extracted verbatim from the (formerly private) Registry SemVer engine
    // (Registry.fs:73-89); pre-release/build metadata are out of scope. Pure, total,
    // exception-free, BCL-only.
    let tryParse (text: string) : Version option =
        match text.Split('.') with
        | [| a; b; c |] ->
            match System.Int32.TryParse a, System.Int32.TryParse b, System.Int32.TryParse c with
            | (true, major), (true, minor), (true, patch) when major >= 0 && minor >= 0 && patch >= 0 ->
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
