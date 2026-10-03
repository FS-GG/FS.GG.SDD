# Literal release readback repair — 2026-10-03

This source-only routine repair continues the TSDD-KNOWLEDGE-01 coherent release lineage.
It changes neither package versions nor the already-retained 2.1.0 archives from protected
`518517f6b90330a6e99f90bbce68faa0a891287f`. No source from this branch is a new publication
candidate. Existing source/version, candidate custody, publisher authentication/occupancy,
package identity, API, public installation, and neutral Q2/Q3 gates remain in place.

## Actual defect and contract

Promotion run `37114843541` attempt 1 published the three original archives to both feeds
but failed public CLI installation while the NuGet resolver still lacked version 2.1.0.
Its logs also exposed a separate readback defect: `unzip -p` interprets the literal name
`[Content_Types].xml` as a pattern, emits no entry bytes, and the shell pipeline hashes
empty output. The successful historical shell payload maps therefore do not qualify this
metadata entry. Native preparation run `37114497970` and artifact `11270818602` preserve the
original source/version/hash inventory independently.

After a fresh check of all three public indexes and literal archive payloads, the authorized
single failed-job rerun succeeded at the same protected source. Attempt 2 passed publisher
custody and exact-payload occupancy checks, public Knowledge CLI/NuGet-FSI, clean installs,
and neutral Q2/Q3. Durable receipt artifact `11271269667` retains all six raw feed archives.
An independent literal ZIP comparison of those archives with the retained originals includes
`[Content_Types].xml`; historical shell maps alone remain insufficient. This recovery did
not repack, change source identity, reserve a version, or create a tag.

The repair uses `zipfile.ZipFile` to read each exact entry name, hashes all entries except
the root `.signature.p7s`, rejects duplicate entries before exclusions, and refuses missing
content types, missing or extra compared entries, corrupt/unreadable archives, and ambiguous
newline names. It reads and compares all supplied archives before writing the existing
`.entries` and `.payloads` receipts. Both coherent promotion and read-only dual-feed
verification call the same helper; their original source-binding checks remain intact.

## Qualification and scope

Fifteen portable command controls exercise equality, ordering, signature-only differences,
actual metadata and ordinary content changes, missing/extra entries, duplicate ordinary and
excluded entries, nested signatures, missing/malformed archives, ambiguous names, and a changed third
archive. The same fifteen controls pass independently against each actual native original
Artifacts, CLI, and Knowledge 2.1.0 archive (60 total observations). Controls use disposable
scratch and do not import the helper or generate repository bytecode. The required native
gate invokes the portable fixture; the helper is not a new SDK or public workflow API.

Local source checks cover this Python/helper/workflow change. Protected hosted source checks
remain required after admission; they are distinct from the old-source publication run.
Root separately owns canonical progress, public consumer adoption, and final independent
six-archive review. Telemetry is not configured; usage/cost are Unknown, with no measured
saving claimed.
