# FSC-04 physical source race characterization

## Scope

The provisional `GenerationSourceSnapshot.capture` reads one caller-selected closed root. Its Linux `statx` probes reject static symlinks, FIFOs, Unix sockets, and other nonregular entries; #1004 also rejects linked workspace ancestors. The probes and `File.ReadAllBytes` are separate path-based operations. A producer cannot infer race-free custody from them.

## Controlled interleaving

An internal injected-reader seam exposes the byte-read boundary to a local test. The test lets both type probes see a regular declared file, briefly replaces that path with a symlink during the read, restores it, and observes a successful capture of bytes from a different file. Requiring `Symlink` in the temporary red-before control failed 1/2 tests; the committed characterization records the current successful capture and the original path's restored bytes. A separate Unix-socket fixture confirms static special-file refusal.

## Follow-up boundary

Before any installed producer uses physical capture as authority, a separate adapter must pin the workspace and source tree with no-follow file descriptors, bind file identity to the bytes read, and define behavior for concurrent directory mutation. Rechecking a path after a read cannot exclude a swap-and-restore interleaving. This draft writes no generated output and does not alter publication, receiver pins, or cutover.
