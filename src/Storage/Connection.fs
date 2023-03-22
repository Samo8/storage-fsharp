namespace Connection

open System.Net.Http

[<AutoOpen>]
module Connection =
    type StorageConnection = {
        Url: string
        Headers: Map<string, string>
        HttpClient: HttpClient
    }
    
    type StorageConnectionBuilder() =
        member _.Yield _ =
            {   Url = ""
                Headers = Map []
                HttpClient = new HttpClient() }
       
        [<CustomOperation("url")>]
        member _.Url(connection, url) =
            { connection with Url = url }
        
        [<CustomOperation("headers")>]
        member _.Headers(connection, headers) =
            { connection with Headers = headers }
            
        [<CustomOperation("httpClient")>]
        member _.HttpClient(connection, httpClient) =
            { connection with HttpClient = httpClient }
            
    let storageConnection = StorageConnectionBuilder()