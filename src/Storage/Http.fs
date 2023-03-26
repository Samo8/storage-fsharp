namespace StorageHttp

open FSharp.Json
open System.Net
open System.Net.Http

open Common
open Connection

[<AutoOpen>]
module Http =
    let private getResponseBody (responseMessage: HttpResponseMessage): string = 
        responseMessage.Content.ReadAsStringAsync()
        |> Async.AwaitTask
        |> Async.RunSynchronously
            
    let deserializeResponse<'T> (response: Result<HttpResponseMessage, StorageError>): Result<'T, StorageError> =        
        match response with
        | Ok r    ->
            printfn $"${r |> getResponseBody}"
            Result.Ok (Json.deserialize<'T> (r |> getResponseBody))
        | Error e -> Result.Error e
        
    let deserializeEmptyResponse (response: Result<HttpResponseMessage, StorageError>): Result<unit, StorageError> =
        match response with
        | Ok _    -> Result.Ok ()
        | Error e -> Result.Error e
        
    let executeHttpRequest (headers: Map<string, string> option) (requestMessage: HttpRequestMessage)
                           (connection: StorageConnection): Result<HttpResponseMessage, StorageError> =
        try
            let httpClient = connection.HttpClient
            let result =
                task {
                    requestMessage.Headers |> addRequestHeaders connection.Headers
                    
                    match headers with
                    | Some h -> requestMessage.Headers |> addRequestHeaders h
                    | _      -> ()
                    
                    let response = httpClient.SendAsync(requestMessage)
                    return! response
                } |> Async.AwaitTask |> Async.RunSynchronously
            match result.StatusCode with
            | HttpStatusCode.OK -> Result.Ok result
            | statusCode        ->
                Result.Error { message    = result |> getResponseBody
                               statusCode = statusCode }
        with e ->
            Result.Error { message    = e.ToString()
                           statusCode = HttpStatusCode.BadRequest }
            
    let private getRequestMessage (httpMethod: HttpMethod) (url: string) (urlSuffix: string): HttpRequestMessage =
        new HttpRequestMessage(httpMethod, $"{url}/{urlSuffix}")
        
    let get (urlSuffix: string) (headers: Map<string, string> option)
            (connection: StorageConnection): Result<HttpResponseMessage, StorageError> =
        let requestMessage = getRequestMessage HttpMethod.Get connection.Url urlSuffix

        connection |> executeHttpRequest headers requestMessage
        
    let delete (urlSuffix: string) (headers: Map<string, string> option) (content: HttpContent option)
               (connection: StorageConnection): Result<HttpResponseMessage, StorageError> =
        let requestMessage = getRequestMessage HttpMethod.Delete connection.Url urlSuffix
        match content with
        | Some c -> requestMessage.Content <- c
        | _      -> ()
        
        connection |> executeHttpRequest headers requestMessage 
    
    let post (urlSuffix: string) (headers: Map<string, string> option) (content: HttpContent)
             (connection: StorageConnection): Result<HttpResponseMessage, StorageError> =
        let requestMessage = getRequestMessage HttpMethod.Post connection.Url urlSuffix
        requestMessage.Content <- content
        
        connection |> executeHttpRequest headers requestMessage 
            
    let put (urlSuffix: string) (headers: Map<string, string> option) (content: HttpContent)
              (connection: StorageConnection): Result<HttpResponseMessage, StorageError> =
        let requestMessage = getRequestMessage HttpMethod.Put connection.Url urlSuffix
        requestMessage.Content <- content
        
        connection |> executeHttpRequest headers requestMessage 