namespace Storage

open System.Net
open FSharp.Json
open System
open System.Net.Http.Headers

/// Contains helper functions for another modules and shared types
[<AutoOpen>]
module Common =
    /// Represents bucket from storage api
    type Bucket = {
        id:        string
        name:      string
        owner:     string option
        [<JsonField("created_at")>]
        createdAt: DateTime
        [<JsonField("updated_at")>]
        updatedAt: DateTime
        [<JsonField("public")>]
        _public:   bool
    }
    
    /// Represents create bucket response 
    type CreateBucket = {
        name: string
    }
    
    /// Represents error for storage api
    type StorageError = {
        message: string
        statusCode: HttpStatusCode option
    }
    
    /// Represents response with message
    type MessageResponse = {
        message: string
    }
    
    /// Represents file metadata
    type Metadata = {
        name: string option
    }
    
    /// Represents file stored at storage api
    type FileObject = {
        id:             string option
        name:           string
        [<JsonField("bucket_id")>]
        bucketId:       string option
        owner:          string option
        [<JsonField("created_at")>]
        createdAt:      DateTime option
        [<JsonField("updated_at")>]
        updatedAt:      DateTime option
        [<JsonField("last_accessed_at")>]
        lastAccessedAt: string option
        metadata:       Metadata option
        bucket:         Bucket option
    }
    
    /// Represent options for bucket being created
    type BucketOptions = {
        [<JsonField("public")>]
        _public: bool
    }
    
    /// Represents seconds
    [<Measure>] type s
    /// Represents pixels
    [<Measure>] type pixel
    /// Represents percents
    [<Measure>] type percent
    
    /// Adds HttpRequestHeaders to given headers Map
    let internal addRequestHeaders (headers: Map<string, string>) (httpRequestHeaders: HttpRequestHeaders): unit =
        headers |> Seq.iter (fun (KeyValue(k, v)) -> httpRequestHeaders.Add(k, v))