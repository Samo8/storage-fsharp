namespace Storage

open System
open System.Net.Http
open System.Text
open FSharp.Json
open Common
open Connection
open StorageHttp

[<AutoOpen>]
module Client =  
    let listBuckets (connection: StorageConnection): Result<Bucket list, StorageError> =
        let response = get "bucket" None connection
        deserializeResponse<Bucket list> response
        
    let getBucket (id: string) (connection: StorageConnection): Result<Bucket, StorageError> =
        let response = get $"bucket/{id}" None connection
        deserializeResponse<Bucket> response
        
    let createBucket (id: string) (bucketOptions: BucketOptions option)
                     (connection: StorageConnection): Result<CreateBucket, StorageError> =
        let isPublic =
            match bucketOptions with
            | Some s -> s._public
            | _      -> false
            
        let body = Map<string, Object> [
            "id", id
            "name", id
            "public", isPublic
        ]
        let content = new StringContent(Json.serialize body, Encoding.UTF8, "application/json")
        let response = post "bucket" None content connection
        deserializeResponse<CreateBucket> response
        
    let updateBucket (id: string) (bucketOptions: BucketOptions)
                     (connection: StorageConnection): Result<MessageResponse, StorageError> =
        let body = Map<string, Object> [
            "id", id
            "public", bucketOptions._public
        ]
        let content = new StringContent(Json.serialize body, Encoding.UTF8, "application/json")
        let response = put $"bucket/{id}" None content connection
        deserializeResponse<MessageResponse> response
        
    let emptyBucket (id: string) (connection: StorageConnection): Result<MessageResponse, StorageError> =
        let content = new StringContent(Json.serialize [], Encoding.UTF8, "application/json")
        let response = post $"bucket/{id}/empty" None content connection
        deserializeResponse<MessageResponse> response
        
    let deleteBucket (id: string) (connection: StorageConnection): Result<MessageResponse, StorageError> =
        let response = delete $"bucket/{id}" None None connection
        deserializeResponse<MessageResponse> response
        
    let from (id: string) (connection: StorageConnection): StorageFile =
        { connection = connection
          bucketId = id
          headers = None }
    
    let updateBearer (bearer: string) (connection: StorageConnection): StorageConnection =
        let formattedBearer = $"Bearer {bearer}"
        let headers =
            match connection.Headers.ContainsKey "Authorization" with
            | true  ->
                connection.Headers |>
                Seq.map (fun (KeyValue (k, v)) ->
                    match k with
                    | "Authorization" -> (k, formattedBearer)
                    | _               -> (k, v))
                |> Map
            | false ->
                connection.Headers |> Map.add "Authorization" formattedBearer
        { connection with Headers = headers }