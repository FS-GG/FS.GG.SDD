namespace FS.GG.SDD.Commands.Tests

open System
open Xunit

/// Opt-in schedules this test; the maintained namespace runner supplies teardown.
type CatalogNamespaceFactAttribute() as this =
    inherit FactAttribute()

    do
        if Environment.GetEnvironmentVariable("FSGG_SDD_NAMESPACE_REGRESSION") <> "1" then
            this.Skip <-
                "Run through catalog-runtime-validation.py with the pid-namespace backend and FSGG_SDD_NAMESPACE_REGRESSION=1."

