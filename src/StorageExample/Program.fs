open Storage

let baseUrl = "https://<project-id>.supabase.co/storage/v1"
let apiKey = "<api-key>"

let connection = storageConnection {
     url baseUrl
     headers (Map [ "apiKey", apiKey
                    "Authorization", $"Bearer {apiKey}" ] )
}

let result =
    connection
    |> from "bucket-name"
    |> StorageFileApi.download "path-to-file" None
    |> Async.RunSynchronously

match result with
| Ok r    -> printfn $"{r}"
| Error e -> printfn $"{e}"