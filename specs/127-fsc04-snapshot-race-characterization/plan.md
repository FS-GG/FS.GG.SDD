# Plan

Stack source-only characterization on #1004. Add a test reader injection at the existing byte-read call, preserving the production `File.ReadAllBytes` route. Exercise a deterministic path swap and a static Unix socket. Keep the unsafe interleaving visible as a bounded finding rather than claiming a descriptor-based repair.
