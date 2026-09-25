# Plan: Pinned Captured-Byte Ownership Transfer

1. Add a stricter warmed 8 MiB allocation control on #1037 and require it to fail before repair; independently verify the ordinary constructor copies caller arrays.
2. Transfer only the pinned reader's freshly allocated arrays into immutable captured results after byte and digest checks, leaving the path-based reader on the defensive-copy constructor.
3. Verify pinned and supplied-reader immutability, focused allocation controls, full Commands tests and a warning-clean Release build.
4. Open an exact-head stacked draft without output effects or installed-parity claims.
