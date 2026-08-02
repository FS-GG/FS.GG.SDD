# Exact-byte evidence digests

## Requirements

- FR-001: An observed test-report receipt hashes the exact bytes read from its cited artifact.
- FR-002: BOMs, line endings, and every other byte affect the receipt digest; decoding is only for XML parsing.
- FR-003: `sync`, `verify`, and `ship` consume the same exact-byte receipt contract.
- FR-004: A receipt lacking `digestContract: exact-bytes-v1` is legacy and blocks until explicitly re-synced.

## Acceptance

- A UTF-8 BOM + CRLF TRX produces its raw `sha256sum` digest.
- LF and arbitrary binary byte arrays demonstrate byte sensitivity.
- The command path records the same digest as the committed report bytes.
