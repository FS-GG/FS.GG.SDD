# Human Summary Review — Ship Command

The `--text` projection (CommandRendering.renderText) emits, for ship:
command, outcome, shipPath, shipReadiness, shipDisposition, ready/advisory/warning/blocking
counts, verification readiness, per-stage lifecycle readiness (`shipStage.<stage>`),
evidence disposition counts, generated-view state, and nextAction. Every text fact is a
projection of the JSON command report (no new facts). Verified by
`ship text projection uses report facts` and `ship CLI text smoke renders human projection`.
