<!-- SYNTHETIC: a stand-in provider UI skill. The real reference provider's skill set
     lives in FS.GG.Rendering; this fixture carries no rendering-specific identifier and
     only exercises the neutral-root co-tenant fan-out contract (056 US1). -->
# fs-gg-elmish (fixture co-tenant skill)

A provider-owned UI skill the provider writes into the neutral `.agents/skills/` root.
SDD (the sole mirror authority) fans this out byte-identically into `.claude` and `.codex`.
