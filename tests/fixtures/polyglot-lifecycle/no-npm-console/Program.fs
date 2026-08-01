open System.IO

Directory.CreateDirectory("results") |> ignore

File.WriteAllText(
    "results/console.junit.xml",
    "<testsuite tests=\"1\"><testcase name=\"no-npm-console\" /></testsuite>"
)

printfn "no-npm console fixture"
