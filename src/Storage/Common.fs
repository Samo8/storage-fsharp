namespace Common

open System.Net
open FSharp.Json
open System
open System.Net.Http.Headers

[<AutoOpen>]
module Common =
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
    
    type CreateBucket = {
        name: string
    }
    
    type StorageError = {
        message: string
        statusCode: HttpStatusCode option
    }
    
    type MessageResponse = {
        message: string
    }
    
    type Metadata = {
        name: string option
    }
    
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
    
    type BucketOptions = {
        [<JsonField("public")>]
        _public: bool
    }
    
    [<Measure>] type s
    [<Measure>] type pixel
    [<Measure>] type percent
    
    let internal addRequestHeaders (headers: Map<string, string>) (httpRequestHeaders: HttpRequestHeaders): unit =
        headers |> Seq.iter (fun (KeyValue(k, v)) -> httpRequestHeaders.Add(k, v))