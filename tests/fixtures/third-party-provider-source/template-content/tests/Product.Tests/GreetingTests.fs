module ThirdParty.GreetingTests

open Xunit

[<Fact>]
let ``greeting names the product`` () =
    Assert.Equal("Hello from IndependentCode", Greeting.greet "IndependentCode")

[<Fact>]
let ``greeting is literal`` () =
    Assert.Equal("Hello from $(literal);&", Greeting.greet "$(literal);&")
