open Storage

let baseUrl = "https://<project-id>.supabase.co/storage/v1"
let apiKey = "<api-key>"

let connection = storageConnection {
     url baseUrl
     headers (Map [ "apiKey", apiKey
                    "Authorization", $"Bearer {apiKey}" ] )
}

let response =
    connection
    |> from "xxx"
    |> list None None
    |> Async.RunSynchronously

match response with
| Ok r    -> printfn $"{r}"
| Error e -> printfn $"{e}"