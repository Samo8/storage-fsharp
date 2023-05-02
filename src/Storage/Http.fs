namespace Storage

open FSharp.Json
open System.Net
open System.Net.Http
open Storage
open Storage.Common

/// Contains functions for performing http request and serialization/deserialization of data
module Http =
    /// Parses HttpResponseMessage to it's string form
    let private getResponseBody (responseMessage: HttpResponseMessage): string = 
        responseMessage.Content.ReadAsStringAsync()
        |> Async.AwaitTask
        |> Async.RunSynchronously
            
    /// Deserializes given response
    let deserializeResponse<'T> (response: Result<HttpResponseMessage, StorageError>): Result<'T, StorageError> =        
        try
            match response with
            | Ok r    -> Result.Ok (Json.deserialize<'T> (getResponseBody r))
            | Error e -> Result.Error e
        with e -> Error { message = e.Message ; statusCode = None }
        
    /// Deserializes empty (unit) response
    let deserializeEmptyResponse (response: Result<HttpResponseMessage, StorageError>): Result<unit, StorageError> =
        match response with
        | Ok _    -> Result.Ok ()
        | Error e -> Result.Error e
        
    /// Executes http response with given headers, requestMessage and handles possible exceptions
    let executeHttpRequest (headers: Map<string, string> option) (requestMessage: HttpRequestMessage)
                           (connection: StorageConnection): Async<Result<HttpResponseMessage, StorageError>> =
        async {
            try
                let httpClient = connection.HttpClient
                
                requestMessage.Headers |> addRequestHeaders connection.Headers
                        
                match headers with
                | Some h -> addRequestHeaders h requestMessage.Headers
                | _      -> ()
                let! response = httpClient.SendAsync(requestMessage) |> Async.AwaitTask
                match response.StatusCode with
                | HttpStatusCode.OK -> return Result.Ok response
                | statusCode -> return Result.Error { message = getResponseBody response; statusCode = Some statusCode }
            with e -> return Result.Error { message = e.ToString(); statusCode = None }
        }
            
    /// Constructs HttpRequestMessage with given method, url and optional headers
    let private getRequestMessage (httpMethod: HttpMethod) (url: string) (urlSuffix: string)
                                  (headers: Map<string, string> option) (content: HttpContent option)
                                  : HttpRequestMessage =
        let requestMessage = new HttpRequestMessage(httpMethod, $"{url}/{urlSuffix}")
        
        match content with
        | Some c -> requestMessage.Content <- c
        | _      -> ()
        match headers with
        | Some h -> addRequestHeaders h requestMessage.Headers
        | _      -> ()
        
        requestMessage
        
    /// Performs http GET request
    let get (urlSuffix: string) (headers: Map<string, string> option)
            (connection: StorageConnection): Async<Result<HttpResponseMessage, StorageError>> =
        let requestMessage = getRequestMessage HttpMethod.Get connection.Url urlSuffix headers None

        executeHttpRequest headers requestMessage connection
        
    /// Performs http DELETE request
    let delete (urlSuffix: string) (headers: Map<string, string> option) (content: HttpContent option)
               (connection: StorageConnection): Async<Result<HttpResponseMessage, StorageError>> =
        let requestMessage = getRequestMessage HttpMethod.Delete connection.Url urlSuffix headers content
        
        executeHttpRequest headers requestMessage connection 
    
    /// Performs http POSR request
    let post (urlSuffix: string) (headers: Map<string, string> option) (content: HttpContent)
             (connection: StorageConnection): Async<Result<HttpResponseMessage, StorageError>> =
        let requestMessage = getRequestMessage HttpMethod.Post connection.Url urlSuffix headers (Some content)
        
        executeHttpRequest headers requestMessage connection 
            
    /// Performs http PUT request
    let put (urlSuffix: string) (headers: Map<string, string> option) (content: HttpContent)
              (connection: StorageConnection): Async<Result<HttpResponseMessage, StorageError>> =
        let requestMessage = getRequestMessage HttpMethod.Put connection.Url urlSuffix headers (Some content)
        
        executeHttpRequest headers requestMessage connection 