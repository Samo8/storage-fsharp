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
    let listBuckets (connection: StorageConnection) =
        let response = connection |> get "bucket" None
        deserializeResponse<Bucket list> response
        
    let getBucket (id: string) (connection: StorageConnection) =
        let response = connection |> get $"bucket/{id}" None
        deserializeResponse<Bucket> response
        
    let createBucket (id: string) (bucketOptions: BucketOptions option) (connection: StorageConnection) =
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
        let response = connection |> post "bucket" None content
        deserializeResponse<CreateBucket> response
        
    let updateBucket (id: string) (bucketOptions: BucketOptions) (connection: StorageConnection) =
        let body = Map<string, Object> [
            "id", id
            "public", bucketOptions._public
        ]
        let content = new StringContent(Json.serialize body, Encoding.UTF8, "application/json")
        let response = connection |> put $"bucket/{id}" None content
        deserializeResponse<MessageResponse> response
        
    let emptyBucket (id: string) (connection: StorageConnection) =
        let content = new StringContent(Json.serialize [], Encoding.UTF8, "application/json")
        let response = connection |> post $"bucket/{id}/empty" None content
        deserializeResponse<MessageResponse> response
        
    let deleteBucket (id: string) (connection: StorageConnection) =
        let response = connection |> delete $"bucket/{id}" None None
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