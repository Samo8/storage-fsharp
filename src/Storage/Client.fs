namespace Storage

open System
open System.Net.Http
open System.Text
open FSharp.Json
open Common
open Connection
open Storage.Http

/// Contains all functions needed for communication with [Supabase storage](https://supabase.com/docs/guides/storage)
[<AutoOpen>]
module Client =
    /// Lists all available buckets
    let listBuckets (connection: StorageConnection): Result<Bucket list, StorageError> =
        let response = get "bucket" None connection
        deserializeResponse<Bucket list> response
        
    /// Lists bucket by id
    let getBucket (id: string) (connection: StorageConnection): Result<Bucket, StorageError> =
        let response = get $"bucket/{id}" None connection
        deserializeResponse<Bucket> response
        
    /// Creates bucket with given id and optional options
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
        let content = getStringContent (Json.serialize body)
        let response = post "bucket" None content connection
        deserializeResponse<CreateBucket> response
        
    /// Updates bucket with given id and optional options
    let updateBucket (id: string) (bucketOptions: BucketOptions)
                     (connection: StorageConnection): Result<MessageResponse, StorageError> =
        let body = Map<string, Object> [
            "id", id
            "public", bucketOptions._public
        ]
        let content = getStringContent (Json.serialize body)
        let response = put $"bucket/{id}" None content connection
        deserializeResponse<MessageResponse> response
        
    /// Empties bucket with given id
    let emptyBucket (id: string) (connection: StorageConnection): Result<MessageResponse, StorageError> =
        let content = getStringContent (Json.serialize [])
        let response = post $"bucket/{id}/empty" None content connection
        deserializeResponse<MessageResponse> response
        
    /// Deletes bucket with given id
    let deleteBucket (id: string) (connection: StorageConnection): Result<MessageResponse, StorageError> =
        let response = delete $"bucket/{id}" None None connection
        deserializeResponse<MessageResponse> response
        
    /// Returns `StorageFile` from bucket id and connection.
    /// Result can be later used in `StorageFileApi.fs`
    let from (id: string) (connection: StorageConnection): StorageFile =
        { connection = connection
          bucketId = id
          headers = None }
    
    /// Updates Bearer token in connection Header and returns new StorageConnection
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