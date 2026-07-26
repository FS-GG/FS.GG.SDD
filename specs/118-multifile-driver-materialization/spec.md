# Feature Specification: Complete Driver Directory Materialization

**Issue**: FS.GG.SDD#710  
**Change tier**: Tier 1 — packaged artifact layout and scaffold behavior

## Outcome

A fresh or upgraded SDD workspace receives the complete directory declared for every selected
`scope: driver` skill in the pinned `FS.GG.Drivers` schema-v2 manifest. Internal references and
scripts therefore arrive with `SKILL.md` instead of becoming dangling links.

## Requirements

- Parse every schema-v2 `files` row and bind the ordered file list to `tree-sha256`.
- Reject malformed manifests, duplicate or escaping paths, invalid digests/modes, and unsupported
  schema versions.
- Embed and verify the closed directory: every declared member must exist, decode, and match its
  raw-byte digest; undeclared members invalidate the row.
- Materialize every verified member into `.agents`, `.claude`, and `.codex`, preserving declared
  executable files and recording each path/digest in scaffold provenance.
- Treat a provider-owned same-id skill as ownership of the whole directory; never create a mixed
  provider/driver tree.
- Make `doctor` expose missing auxiliary members and make `upgrade --yes` backfill them without
  clobbering present files, updating provenance to include recovered and pre-existing members.

## Acceptance

- Schema-v2 parser tests cover valid, malformed, duplicate, traversal, and tree-digest cases.
- Materializer tests cover missing, extra, unreadable, digest-mismatched, and executable members.
- A real fresh scaffold contains every declared work-board/work-roadmap file in all three roots;
  every relative Markdown link resolves in the composed workspace, including parent-relative
  product-sibling targets.
- A legacy `SKILL.md`-only workspace upgrades to complete directories and complete provenance.

## Boundaries

The package manifest remains the byte/mode authority. SDD embeds no driver id or producer-specific
path in product behavior; it consumes whichever driver rows the pinned package declares.
