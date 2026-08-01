# Polyglot lifecycle acceptance

`tests/fixtures/polyglot-lifecycle/` is the provider-composition regression fixture for a
workspace with independently owned F# and Node/browser lanes. It deliberately does not add a
`language:` field or any TypeScript-specific lifecycle concept: SDD's evidence boundary consumes
runner-produced TRX and JUnit XML by their common report contract.

The acceptance test runs both lanes, copies their reports into one lifecycle work item, and invokes
`evidence --from-test-report` once for each report before the normal `verify` and `ship` stages.
`evidence` only reads the supplied report; it never launches either test suite.

To run the fixture manually:

```sh
dotnet test tests/fixtures/polyglot-lifecycle/server.tests/Polyglot.Server.Tests.fsproj \
  --logger "trx;LogFileName=server.trx" \
  --results-directory tests/fixtures/polyglot-lifecycle/results
npm --prefix tests/fixtures/polyglot-lifecycle/client test --silent
```

The Node command writes `results/client.junit.xml` using Node's built-in JUnit reporter. The
reporter intentionally exercises the valid JUnit shape that has `<testcase>` elements but no
aggregate count attributes; SDD derives counts from those executed cases, rather than requiring a
.NET-style aggregate summary.
