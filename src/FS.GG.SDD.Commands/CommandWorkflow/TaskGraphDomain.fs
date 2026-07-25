namespace FS.GG.SDD.Commands.Internal

open System
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts

/// Pure policy used while deriving a task graph. The command-facing authoring module
/// retains its stable functions and delegates normalization and mirror classification here.
module internal TaskGraphDomain =
    let private resolveSkill (neutral: string) (declared: string option) =
        match declared with
        | Some raw when not (String.IsNullOrWhiteSpace raw) -> Regex.Replace(raw.Trim().ToLowerInvariant(), @"\s+", "-")
        | _ -> neutral

    let resolveTestSkill declared = resolveSkill "automated-tests" declared

    let resolveImplementSkill declared = resolveSkill "implementation" declared

    type DerivedSkills =
        { TestSkill: string
          ImplementSkill: string }

    let derivedSkills (config: ProjectLifecycleConfig option) =
        { TestSkill = resolveTestSkill (config |> Option.bind _.TestFramework)
          ImplementSkill = resolveImplementSkill (config |> Option.bind _.ImplementSkill) }

    let derivedVisualSurface (config: ProjectLifecycleConfig option) =
        config |> Option.map _.VisualSurface |> Option.defaultValue false

    let upperSet (values: string list) =
        values |> List.map _.ToUpperInvariant() |> Set.ofList

    let private deferralMirrorBoilerplate =
        Regex(
            @"^acceptedDeferral:\s*Accepted deferral\s+[A-Za-z][A-Za-z0-9]*-\d{3,}\s+remains visible to task generation\.?\s*$",
            RegexOptions.IgnoreCase
        )

    let isPureDeferralMirror (acceptedDeferralIdSet: Set<string>) (decision: PlanDecision) =
        let refs = upperSet decision.SourceIds
        let withoutRefs = Regex.Replace(decision.Text, @"^\s*(?:\[[^\]]*\]\s*)+", "")

        not (Set.isEmpty refs)
        && Set.isSubset refs acceptedDeferralIdSet
        && decision.Status.Equals("acceptedDeferral", StringComparison.OrdinalIgnoreCase)
        && deferralMirrorBoilerplate.IsMatch(withoutRefs.Trim())

    let isPureChecklistDeferralMirror (clarificationDeferralIdSet: Set<string>) (result: ChecklistReviewResult) =
        let refs = upperSet result.SourceIds

        not (Set.isEmpty refs)
        && Set.isSubset refs clarificationDeferralIdSet
        && result.Status.Equals("acceptedDeferral", StringComparison.OrdinalIgnoreCase)
