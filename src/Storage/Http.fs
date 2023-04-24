namespace Storage

open FSharp.Json
open System.Net
open System.Net.Http
open Storage

/// Contains functions for performing http request and serialization/deserialization of data
[<AutoOpen>]
module Http =
    /// Parses HttpResponseMessage to it's string form
    let private getResponseBody (responseMessage: HttpResponseMessage): string = 
        responseMessage.Content.ReadAsStringAsync()
        |> Async.AwaitTask
        |> Async.RunSynchronously
            
    /// Deserializes given response
    let deserializeResponse<'T> (response: Result<HttpResponseMessage, StorageError>): Result<'T, StorageError> =        
        match response with
        | Ok r    ->
            printfn $"${r |> getResponseBody}"
            Result.Ok (Json.deserialize<'T> (getResponseBody r))
        | Error e -> Result.Error e
        
    /// Deserializes empty (unit) response
    let deserializeEmptyResponse (response: Result<HttpResponseMessage, StorageError>): Result<unit, StorageError> =
        match response with
        | Ok _    -> Result.Ok ()
        | Error e -> Result.Error e
        
    /// Executes http response with given headers, requestMessage and handles possible exceptions
    let executeHttpRequest (headers: Map<string, string> option) (requestMessage: HttpRequestMessage)
                           (connection: StorageConnection): Result<HttpResponseMessage, StorageError> =
        try
            let httpClient = connection.HttpClient
            let result =
                task {
                    requestMessage.Headers |> addRequestHeaders connection.Headers
                    
                    match headers with
                    | Some h -> addRequestHeaders h requestMessage.Headers
                    | _      -> ()
                    
                    let response = httpClient.SendAsync(requestMessage)
                    return! response
                } |> Async.AwaitTask |> Async.RunSynchronously
            match result.StatusCode with
            | HttpStatusCode.OK -> Result.Ok result
            | statusCode        ->
                Result.Error { message    = getResponseBody result
                               statusCode = Some statusCode }
        with e ->
            Result.Error { message    = e.ToString()
                           statusCode = None }
            
    /// Constructs HttpRequestMessage with given method and url
    let private getRequestMessage (httpMethod: HttpMethod) (url: string) (urlSuffix: string): HttpRequestMessage =
        new HttpRequestMessage(httpMethod, $"{url}/{urlSuffix}")
        
    /// Performs http GET request
    let get (urlSuffix: string) (headers: Map<string, string> option)
            (connection: StorageConnection): Result<HttpResponseMessage, StorageError> =
        let requestMessage = getRequestMessage HttpMethod.Get connection.Url urlSuffix

        executeHttpRequest headers requestMessage connection
        
    /// Performs http DELETE request
    let delete (urlSuffix: string) (headers: Map<string, string> option) (content: HttpContent option)
               (connection: StorageConnection): Result<HttpResponseMessage, StorageError> =
        let requestMessage = getRequestMessage HttpMethod.Delete connection.Url urlSuffix
        match content with
        | Some c -> requestMessage.Content <- c
        | _      -> ()
        
        executeHttpRequest headers requestMessage connection 
    
    /// Performs http POSR request
    let post (urlSuffix: string) (headers: Map<string, string> option) (content: HttpContent)
             (connection: StorageConnection): Result<HttpResponseMessage, StorageError> =
        let requestMessage = getRequestMessage HttpMethod.Post connection.Url urlSuffix
        requestMessage.Content <- content
        
        executeHttpRequest headers requestMessage connection 
            
    /// Performs http PUT request
    let put (urlSuffix: string) (headers: Map<string, string> option) (content: HttpContent)
              (connection: StorageConnection): Result<HttpResponseMessage, StorageError> =
        let requestMessage = getRequestMessage HttpMethod.Put connection.Url urlSuffix
        requestMessage.Content <- content
        
        executeHttpRequest headers requestMessage connection 