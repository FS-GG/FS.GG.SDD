# Generic Quint replay ownership

The 2.0.2 extraction preview delegates generic replay behavior to public FsQuint
0.1.0-preview.1. Existing SDD public records/unions, CLR namespace and schema-v1
canonical bytes remain in place. The facade maps data; it contains no JSON parser,
canonical encoder, fingerprint implementation, validator or comparator.

FsQuint owns generic defects and releases. SDD retains its compiler, profiles,
lifecycle and action/source policy. Pin updates are reviewed and tested; Quint CLI
versions are independent. Restore requires only nuget.org. The temporary facade
is retired only by a separately reviewed major SDD API transition.

This preview is intentional under FSQUINT-01; it does not change the stable 2.0.1
channel for ordinary consumers. Stable adoption follows production-consumer and
upstream-update qualification. Preview 1's malformed UTF-16 fingerprint fallback
is tracked upstream for the next preview; valid Unicode vectors remain unchanged.

Validation includes the existing replay fixture, full artifact tests, and an
unchanged client compiled against public 2.0.1 then executed with the candidate
assembly. `scripts/quint-replay-binary-compat.sh` performs that binary check. Release
projection and golden changes are version/channel and their derived digest changes;
no fixture is refreshed to accept changed replay observations.
