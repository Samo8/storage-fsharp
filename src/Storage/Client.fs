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
    let listBuckets (connection: StorageConnection): Async<Result<Bucket list, StorageError>> =
        async {
            let! response = get "bucket" None connection
            return deserializeResponse<Bucket list> response
        }
        
    /// Lists bucket by id
    let getBucket (id: string) (connection: StorageConnection): Async<Result<Bucket, StorageError>> =
        async {
            let! response = get $"bucket/{id}" None connection
            return deserializeResponse<Bucket> response
        }
        
    /// Creates bucket with given id and optional options
    let createBucket (id: string) (bucketOptions: BucketOptions option)
                     (connection: StorageConnection): Async<Result<CreateBucket, StorageError>> =
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
        
        async {
            let! response = post "bucket" None content connection
            return deserializeResponse<CreateBucket> response
        }
        
    /// Updates bucket with given id and optional options
    let updateBucket (id: string) (bucketOptions: BucketOptions)
                     (connection: StorageConnection): Async<Result<MessageResponse, StorageError>> =
        let body = Map<string, Object> [
            "id", id
            "public", bucketOptions._public
        ]
        let content = getStringContent (Json.serialize body)
        
        async {
            let! response = put $"bucket/{id}" None content connection
            return deserializeResponse<MessageResponse> response
        }
        
    /// Empties bucket with given id
    let emptyBucket (id: string) (connection: StorageConnection): Async<Result<MessageResponse, StorageError>> =
        let content = getStringContent (Json.serialize [])
        
        async {
            let! response = post $"bucket/{id}/empty" None content connection
            return deserializeResponse<MessageResponse> response
        }
        
    /// Deletes bucket with given id
    let deleteBucket (id: string) (connection: StorageConnection): Async<Result<MessageResponse, StorageError>> =
        async {
            let! response = delete $"bucket/{id}" None None connection
            return deserializeResponse<MessageResponse> response
        }
        
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