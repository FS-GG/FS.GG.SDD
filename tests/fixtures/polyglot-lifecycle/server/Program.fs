open Microsoft.AspNetCore.Builder

let builder = WebApplication.CreateBuilder()
let app = builder.Build()
app.MapGet("/health", System.Func<string>(fun () -> "polyglot server ready")) |> ignore
app.Run()
