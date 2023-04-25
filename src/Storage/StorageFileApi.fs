namespace Storage

open System.Net.Http
open System.Text
open FSharp.Json
open Common
open Connection
open Storage.Http

/// Contains helper functions used by `StorageFileApi` module
[<AutoOpen>]
module StorageFileApiHelper =
    /// Represents file returned by Storage
    type StorageFile = {
        connection: StorageConnection
        bucketId: string
        headers: Map<string, string> option
    }
    
    /// Represents ordering options
    type OrderBy = Ascending | Descending
    
    /// Represents column to sort by and ordering option 
    type SortBy = {
        column: string
        order:  OrderBy
    }
    
    /// Represents file path
    type Path = string
    
    /// Represents possible sort options parameters
    type SearchOptions = {
        limit:  int option
        offset: int option
        sortBy: SortBy option
        search: string option
    }
    
    /// Represents file response returned by api
    type FileResponse = {
        [<JsonField("Key")>]
        key: string
    }
    
    /// Represents response for creating signed url
    type SignUrlResponse = {
        [<JsonField("signedURL")>]
        signedUrl: string
    }
    
    /// Represents resize modes for image
    type ResizeMode = COVER | CONTAIN | FILL
    
    /// Represents transforming options for image
    type TransformOptions = {
        height: int<pixel> option
        width: int<pixel> option
        resize: ResizeMode Option
        format: string option
        quality: int<percent> option
    }
    
    /// Returns string representation of `OrderBy`
    let getOrderByValue (orderBy: OrderBy): string =
        match orderBy with
        | Ascending  -> "asc"
        | Descending -> "desc"
        
    /// Parses optional search options to map representation.
    /// If no search options is given then uses default values
    let parseSearchOptions (path: Path option) (searchOptions: SearchOptions Option) =
        let prefix = ("", path) ||> Option.defaultValue
        
        match searchOptions with
        | Some options ->
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
        | _  ->
            Map<string, obj>["prefix", prefix]
                
    /// Helper function for performing move or copy operation 
    let moveOrCopy<'T> (fromPath: Path) (toPath: Path) (action: string) (storageFileApi: StorageFile) =
        match action with
        | "move" | "copy" ->
            let body =
                Map<string, string>
                    [ "bucketId",       storageFileApi.bucketId
                      "sourceKey",      fromPath
                      "destinationKey", toPath ]
                    
            let content = getStringContent (Json.serialize body)
            let response = post $"object/{action}" None content storageFileApi.connection
            deserializeResponse<'T> response
        | _ -> Error { message = $"Unsupported action {action}, use move/copy action!"
                       statusCode = None }
        
    /// Adds key-value pair to map if value is given
    let addTransformValueIfPresent (key: string) (value: 'a option) (map: Map<string, string>) =
        match value with
        | Some v -> map |> Map.add key (v.ToString().ToLower())
        | _      -> map
    
    /// Gets Map representation of optional transform options 
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
            
    /// Returns transform options as url params
    let transformOptionsToUrlParams (transform: TransformOptions option): string =
        let paramsMap = getTransformOptionsParamsMap transform
        match paramsMap.IsEmpty with
        | false ->
            let urlParams = paramsMap |> Map.toList
            let headKey, headValue = urlParams.Head
            
            ($"?{headKey}={headValue}", urlParams.Tail)
            ||> List.fold (fun acc (key, value) -> acc + $"&{key}={value}")
        | _     -> ""
        
    let getFullFilePath (bucketId: string) (path: string) = $"{bucketId}/{path}"

/// Contains function for communication with Storage file api
[<AutoOpen>]
module StorageFileApi =
    /// Lists files at given path filtered by given search options
    let list (path: Path option) (searchOptions: SearchOptions option)
             (storageFileApi: StorageFile): Result<FileObject list, StorageError> =
        let body = parseSearchOptions path searchOptions

        let content = getStringContent (Json.serialize body)
        let response = post $"object/list/{storageFileApi.bucketId}" None content storageFileApi.connection
        deserializeResponse<FileObject list> response
    
    /// Moves file from given location to given location
    let move (fromPath: Path) (toPath: Path)
             (storageFileApi: StorageFile): Result<MessageResponse, StorageError> =
        moveOrCopy<MessageResponse> fromPath toPath "move" storageFileApi
            
    /// Copies file from given location to given location
    let copy (fromPath: Path) (toPath: Path)
             (storageFileApi: StorageFile): Result<FileResponse, StorageError> =
        moveOrCopy<FileResponse> fromPath toPath "copy" storageFileApi
    
    /// Creates signed url for file at given path with given options
    let createSignedUrl (path: string) (expiresIn: int<s>) (transform: TransformOptions option)
                        (storageFileApi: StorageFile): Result<string, StorageError> =
        let transformValue = getTransformOptionsParamsMap transform
        let body =
            Map<string, obj>
                [ "expiresIn", expiresIn
                  "transform", transformValue ]
        let content = getStringContent (Json.serialize body)
        
        let fullPath = getFullFilePath storageFileApi.bucketId path
        
        let response = post $"object/sign/{fullPath}" None content storageFileApi.connection
        let deserializedResponse = deserializeResponse<SignUrlResponse> response
        match deserializedResponse with
        | Ok r    -> Ok $"{storageFileApi.connection.Url}{r.signedUrl}"
        | Error e -> Error e
        
    /// Creates signed urls for files at given path with given options
    let createSignedUrls (paths: Path list) (expiresIn: int<s>) (storageFileApi: StorageFile) =
        let body =
            Map<string, obj>
                [ "expiresIn", expiresIn
                  "paths", paths ]
        let content = getStringContent (Json.serialize body)
        
        let response = post $"object/sign/{storageFileApi.bucketId}" None content storageFileApi.connection
        let deserializedResponse = deserializeResponse<SignUrlResponse list> response
        match deserializedResponse with
        | Ok r    ->
            Ok (r |> List.map (fun url -> $"{storageFileApi.connection.Url}{url.signedUrl}"))
        | Error e -> Error e
    
    /// Downloads file at given given path with given transform options
    let download (path: Path) (transform: TransformOptions option) (storageFileApi: StorageFile) =
        let renderPath = if transform.IsSome then "render/image/authenticated" else "object"
        let urlParams = transformOptionsToUrlParams transform
        
        let fullPath = getFullFilePath storageFileApi.bucketId path
        
        let response = get $"{renderPath}/{fullPath}{urlParams}" None storageFileApi.connection
        match response with
        | Ok r ->
            let file =
                task {
                    return! r.Content.ReadAsByteArrayAsync()
                } |> Async.AwaitTask |> Async.RunSynchronously
            Ok file
        | Error e -> Error e    
            
    /// Gets public url for public file at given path
    let getPublicUrl (path: Path) (transform: TransformOptions option) (storageFileApi: StorageFile) =
        let renderPath = if transform.IsSome then "render/image" else "object"
        let urlParams = transformOptionsToUrlParams transform
        
        let fullPath = getFullFilePath storageFileApi.bucketId path

        $"{storageFileApi.connection.Url}/{renderPath}/public/{fullPath}{urlParams}"
        
    /// Removes files at given paths
    let remove (paths: Path list) (storageFileApi: StorageFile) =
        let body = Map<string, obj>[ "prefixes", paths ]
            
        let content = getStringContent (Json.serialize body)
        let response = delete $"object/{storageFileApi.bucketId}" None (Some content) storageFileApi.connection
        deserializeResponse<FileObject list> response
        
    /// Uploads given file to given path
    let upload (path: Path) (file: byte[]) (storageFileApi: StorageFile) =    
        let content = new ByteArrayContent(file)
        
        let fullPath = getFullFilePath storageFileApi.bucketId path
        
        let response = post $"object/{fullPath}" None content storageFileApi.connection
        deserializeResponse<FileResponse> response
        
    /// Replaces file at given path by given file
    let update (path: Path) (file: byte[]) (storageFileApi: StorageFile) =    
        let content = new ByteArrayContent(file)
        
        let fullPath = getFullFilePath storageFileApi.bucketId path
        
        let response = put $"object/{fullPath}" None content storageFileApi.connection
        deserializeResponse<FileResponse> response