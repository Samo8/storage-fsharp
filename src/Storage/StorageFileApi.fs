namespace Storage

open System.Net.Http
open System.Text
open FSharp.Json
open Common
open Connection
open StorageHttp

[<AutoOpen>]
module StorageFileApiHelper =
    type StorageFile = {
        connection: StorageConnection
        bucketId: string
        headers: Map<string, string> option
    }
    
    type OrderBy = Ascending | Descending
    
    type SortBy = {
        column: string
        order:  OrderBy
    }
    
    type SearchOptions = {
        limit:  int option
        offset: int option
        sortBy: SortBy option
        search: string option
    }
    
    type FileResponse = {
        [<JsonField("Key")>]
        key: string
    }
    
    type SignUrlResponse = {
        [<JsonField("signedURL")>]
        signedUrl: string
    }
    
    type ResizeMode = COVER | CONTAIN | FILL
    
    type TransformOptions = {
        height: int<pixel> option
        width: int<pixel> option
        resize: ResizeMode Option
        format: string option
        quality: int<percent> option
    }
    
    let getOrderByValue (orderBy: OrderBy): string =
        match orderBy with
        | Ascending  -> "asc"
        | Descending -> "desc"
        
    let inline parseSearchOptions (path: string option) (searchOptions: SearchOptions Option) =
        match searchOptions with
        | Some options ->
            let prefix = ("", path) ||> Option.defaultValue
            let limit = (100, options.limit) ||> Option.defaultValue
            let offset = (0, options.offset) ||> Option.defaultValue
            let search = ("", options.search) ||> Option.defaultValue
            
            Map<string, obj>
                [ "prefix", prefix
                  "limit", limit
                  "offset", offset
                  "sortBy",
                    match options.sortBy with
                    | Some sortBy ->
                        Map<string, string>
                            [ "column", sortBy.column
                              "order",  getOrderByValue sortBy.order ]
                    | _           -> Map<string, string>[]
                  "search", search ]
        | _  -> Map<string, obj>["prefix", ""]
                
    let moveOrCopy<'T> (fromPath: string) (toPath: string) (storageFileApi: StorageFile) (action: string) =
        let body =
            Map<string, string>
                [ "bucketId",       storageFileApi.bucketId
                  "sourceKey",      fromPath
                  "destinationKey", toPath ]
                
        let content = new StringContent(Json.serialize(body), Encoding.UTF8, "application/json")
        let response = storageFileApi.connection |> post $"object/{action}" None content
        response |> deserializeResponse<'T>
        
    let addTransformValueIfPresent (key: string) (value: 'a option) (map: Map<string, string>) =
        match value with
        | Some v -> map |> Map.add key (v.ToString().ToLower())
        | _      -> map
    
    let getTransformOptionsParamsMap (transform: TransformOptions option) =
        match transform with
        | Some t ->
            Map.empty<string, string>
            |> addTransformValueIfPresent "height" t.height
            |> addTransformValueIfPresent "width" t.width
            |> addTransformValueIfPresent "resize" t.resize
            |> addTransformValueIfPresent "format" t.format
            |> addTransformValueIfPresent "quality" t.quality
        | _      -> Map.empty<string, string>
            
    let transformOptionsToUrlParams (transform: TransformOptions option): string =
        let paramsMap = transform |> getTransformOptionsParamsMap
        match paramsMap.IsEmpty with
        | false ->
            let urlParams = paramsMap |> Map.toList
            let headKey, headValue = urlParams.Head
            urlParams.Tail |> List.fold (fun acc (key, value) -> acc + $"&{key}={value}") $"?{headKey}={headValue}"
        | _     -> ""
        
    let getFullFilePath (bucketId: string) (path: string) = $"{bucketId}/{path}"

[<AutoOpen>]
module StorageFileApi =
    let list (path: string option) (searchOptions: SearchOptions option) (storageFileApi: StorageFile) =
        let body = searchOptions |> parseSearchOptions path

        let content = new StringContent(Json.serialize(body), Encoding.UTF8, "application/json")
        let response = storageFileApi.connection |> post $"object/list/{storageFileApi.bucketId}" None content
        response |> deserializeResponse<FileObject list>
    
    let move (fromPath: string) (toPath: string) (storageFileApi: StorageFile) =
        "move" |> moveOrCopy<MessageResponse> fromPath toPath storageFileApi
            
    let copy (fromPath: string) (toPath: string) (storageFileApi: StorageFile) =
        "copy" |> moveOrCopy<FileResponse> fromPath toPath storageFileApi
    
    let createSignedUrl (path: string) (expiresIn: int<s>) (transform: TransformOptions option) (storageFileApi: StorageFile) =
        let transformValue = transform |> getTransformOptionsParamsMap
        let body =
            Map<string, obj>
                [ "expiresIn", expiresIn
                  "transform", transformValue ]
        let content = new StringContent(Json.serialize(body), Encoding.UTF8, "application/json")
        
        let fullPath = getFullFilePath storageFileApi.bucketId path
        
        let response = storageFileApi.connection |> post $"object/sign/{fullPath}" None content
        let deserializedResponse = response |> deserializeResponse<SignUrlResponse>
        match deserializedResponse with
        | Ok r    -> Ok $"{storageFileApi.connection.Url}{r.signedUrl}"
        | Error e -> Error e
        
    let createSignedUrls (paths: string list) (expiresIn: int<s>) (storageFileApi: StorageFile) =
        let body =
            Map<string, obj>
                [ "expiresIn", expiresIn
                  "paths", paths ]
        let content = new StringContent(Json.serialize(body), Encoding.UTF8, "application/json")
        
        let response = storageFileApi.connection |> post $"object/sign/{storageFileApi.bucketId}" None content
        let deserializedResponse = response |> deserializeResponse<SignUrlResponse list>
        match deserializedResponse with
        | Ok r    ->
            Ok (r |> List.map (fun url -> $"{storageFileApi.connection.Url}{url.signedUrl}"))
        | Error e -> Error e
    
    let download (path: string) (transform: TransformOptions option) (storageFileApi: StorageFile) =
        let renderPath = if transform.IsSome then "render/image/authenticated" else "object"
        let urlParams = transform |> transformOptionsToUrlParams
        
        let fullPath = getFullFilePath storageFileApi.bucketId path
        
        let response = storageFileApi.connection |> get $"{renderPath}/{fullPath}{urlParams}" None
        match response with
        | Ok r ->
            let file =
                task {
                    return! r.Content.ReadAsByteArrayAsync()
                } |> Async.AwaitTask |> Async.RunSynchronously
            Ok file
        | Error e -> Error e    
            
    let getPublicUrl (path: string) (transform: TransformOptions option) (storageFileApi: StorageFile) =
        let renderPath = if transform.IsSome then "render/image" else "object"
        let urlParams = transform |> transformOptionsToUrlParams
        
        let fullPath = getFullFilePath storageFileApi.bucketId path

        $"{storageFileApi.connection.Url}/{renderPath}/public/{fullPath}{urlParams}"
        
    let remove (paths: string list) (storageFileApi: StorageFile) =
        let body = Map<string, obj>[ "prefixes", paths ]
            
        let content = new StringContent(Json.serialize(body), Encoding.UTF8, "application/json")
        let response = storageFileApi.connection |> delete $"object/{storageFileApi.bucketId}" None (Some content)
        response |> deserializeResponse<FileObject list>
        
    let upload (path: string) (file: byte[]) (storageFileApi: StorageFile) =    
        let content = new ByteArrayContent(file)
        
        let fullPath = getFullFilePath storageFileApi.bucketId path
        
        let response = storageFileApi.connection |> post $"object/{fullPath}" None content
        response |> deserializeResponse<FileResponse>
        
    let update (path: string) (file: byte[]) (storageFileApi: StorageFile) =    
        let content = new ByteArrayContent(file)
        
        let fullPath = getFullFilePath storageFileApi.bucketId path
        
        let response = storageFileApi.connection |> put $"object/{fullPath}" None content
        response |> deserializeResponse<FileResponse>