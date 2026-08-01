open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

let builder = WebApplication.CreateBuilder()
let app = builder.Build()

app.MapGet("/health", fun (context: HttpContext) -> context.Response.WriteAsync("polyglot server ready"))
|> ignore

app.Run()
