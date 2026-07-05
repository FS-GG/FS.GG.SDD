// ROOT_NAMESPACE is an *identifier* context — it must be a valid F# namespace. The raw
// product name "PRODUCT_NAME" appears only in this string literal (a string/display context).
namespace ROOT_NAMESPACE

module Program =
    [<EntryPoint>]
    let main _ =
        printfn "PRODUCT_NAME"
        0
