# Typed SDD exact tool provisioning

Profile `fsgg-quint-profile/2` uses a fixed Linux/amd64 tool set. Acquisition is explicit and online;
`author` and `inspect` remain offline consumers. The installed provisioner accepts local files, verifies
their manifest SHA256 and size before writing, and stages them at `objects/<sha256>` under the selected
cache. Its JSON report records the installed CLI package, platform, profile, upstream identity, local source
path, SHA256, size and final cache path.

The public 1.6.0 CLI was checked first. With an empty cache, its profile-2 `author` command refused
`lmt-binary` and `quint-binary` through `typedSdd.v2.cacheInvalid`, as designed. The installed surface offered
only `author`, `inspect`, `migrate` and `rollback`; documentation told consumers to preseed `cache/objects`,
while acquisition existed only as duplicated gate/release workflow code. Rebuilding the accepted lmt object
with the documented Go version exposed one missing build input: hosted runners implicitly used CGO, while a
clean official Go installation defaulted it off and produced SHA256
`a9d261ff65466855946a7b7db2885161983d17eb1aa88fd11e0d69de7e0ecf3a`. Pinning `CGO_ENABLED=1` reproduced
the accepted hash. The provision operation and recipe below close only that installed-consumer gap.

Acquire Quint 0.32.0 directly from its release and verify it:

```sh
curl --fail --location --silent --show-error \
  https://github.com/quint-co/quint/releases/download/v0.32.0/quint-linux-amd64 \
  --output quint-linux-amd64
printf '%s  %s\n' \
  939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f \
  quint-linux-amd64 | sha256sum --check --strict
chmod +x quint-linux-amd64
```

The accepted literate extractor is `driusan/lmt` commit
`62fe18f2f6a6e11c158ff2b2209e1082a4fcd59c`. Its vendored `main.go` SHA256 is
`88bc47acae2c26919ab96a5cafa80b12fac762092c57840a2baad1afcc7feda3`. Build it with the official
Go 1.24.1 Linux/amd64 archive, SHA256
`cb2396bae64183cdccf81a9a6df0aea3bce9511fc21469fb89a0c00470088073`. `CGO_ENABLED=1` is part of
the qualified build input; omitting it produces different bytes on hosts where Go defaults CGO off.

```sh
curl --fail --location --silent --show-error \
  https://go.dev/dl/go1.24.1.linux-amd64.tar.gz --output go1.24.1.linux-amd64.tar.gz
printf '%s  %s\n' \
  cb2396bae64183cdccf81a9a6df0aea3bce9511fc21469fb89a0c00470088073 \
  go1.24.1.linux-amd64.tar.gz | sha256sum --check --strict
tar -xzf go1.24.1.linux-amd64.tar.gz
curl --fail --location --silent --show-error \
  https://raw.githubusercontent.com/driusan/lmt/62fe18f2f6a6e11c158ff2b2209e1082a4fcd59c/main.go \
  --output main.go
printf '%s  %s\n' \
  88bc47acae2c26919ab96a5cafa80b12fac762092c57840a2baad1afcc7feda3 \
  main.go | sha256sum --check --strict
PATH="$PWD/go/bin:$PATH" GO111MODULE=off CGO_ENABLED=1 go build -trimpath \
  -ldflags '-buildid=IvXAt1kJ-3iINki1alCT/Ut12KGabgkWIkwVpw-xO/c4zkZMLAubfWHvjZOY8o/8-oR_8tNNndNgfMVoD8F -B 0x03d1703027f57ed4dd2ba90b7cdfc8cdea2815da' \
  -o lmt main.go
printf '%s  %s\n' \
  37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10 \
  lmt | sha256sum --check --strict
```

Commit the complete set through the installed CLI:

```sh
fsgg-sdd typed-sdd provision --cache "$HOME/.cache/fsgg/quint" \
  --quint ./quint-linux-amd64 --lmt ./lmt > provision-report.json
```

The operation supports only its declared profile and platform. Missing or modified inputs, a wrong profile,
an invalid path and conflicting bytes already at a content-addressed target are refused with stable
`typedSdd.provision.*` diagnostics. Inputs are fully checked before cache mutation. Concurrent invocations
serialize through a cache-local lock; each invocation stages both files before content-addressed moves and
rolls back files it created on an observed write failure. A process crash can expose an incomplete set, which
`author` refuses; rerunning provision safely completes the set from verified inputs. Existing exact objects are
retained and their executable mode is repaired. Profile-1 workspaces do not use or change this cache contract.

Fable is outside the author/inspect cache and remains an explicit correspondence-test dependency. Install
`fable` 5.13.0 in the isolated qualification tool path; do not accept an ambient version:

```sh
dotnet tool install fable --version 5.13.0 --tool-path ./fable-5.13.0
./fable-5.13.0/fable --version
```
