namespace FS.GG.SDD.Commands

open System

/// The closed set of SDK and runtime pairs the catalog transport and the provider-source edge accept
/// (SDK-PATCH-01, FS-GG/.github#4935 Q5). Each pair is exact: SDK 10.0.401 ships runtime 10.0.12 and SDK
/// 10.0.403 ships runtime 10.0.14. Neither a floating band nor an SDK paired with another pair's runtime is
/// accepted, and a pair whose bytes are not installed is refused by input capture. Adding a pair is a
/// separately qualified decision, never a range. Deliberately without an .fsi: it is internal and adds no
/// authored public surface.
module internal CatalogHostProfile =
    type HostProfile = { Sdk: string; Runtime: Version }

    let hostProfiles: HostProfile list =
        [
            {
                Sdk = "10.0.401"
                Runtime = Version(10, 0, 12)
            }
            {
                Sdk = "10.0.403"
                Runtime = Version(10, 0, 14)
            }
        ]

    /// The profile whose runtime is exactly the supplied host runtime; None refuses the host.
    let tryHostProfile (runtime: Version) : HostProfile option =
        hostProfiles |> List.tryFind (fun profile -> profile.Runtime = runtime)
