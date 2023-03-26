open System
open System.Net
open System.Net.Http
open System.Threading
open Storage
open FsUnit.Xunit
open Moq
open Moq.Protected
open Xunit
open Helper
open Common
open Connection

module StorageFileApiHelperTests =
    [<Collection("getOrderByValue")>]
    module GetOrderByValueTests =
        [<Fact>]
        let ``should return asc when given Ascending OrderBy`` () =
            // Arrange
            let orderBy = Ascending
            let expected = "asc"
            
            // Act
            let result = StorageFileApiHelper.getOrderByValue orderBy
            
            // Assert
            result |> should equal expected
            
        [<Fact>]
        let ``should return desc when given Descending OrderBy`` () =
            // Arrange
            let orderBy = Descending
            let expected = "desc"
            
            // Act
            let result = StorageFileApiHelper.getOrderByValue orderBy
            
            // Assert
            result |> should equal expected
    
    [<Collection("parseSearchOptions")>]
    module ParseSearchOptionsTests =
        [<Fact>]
        let ``given None path and None search options, should return a map containing empty prefix`` () =
            // Arrange
            let path = None
            let searchOptions = None
            
            let expected = Map<string, obj>["prefix", ""]
            
            // Act
            let result = StorageFileApiHelper.parseSearchOptions path searchOptions
            
            // Assert
            result |> should equal expected
            
        [<Fact>]
        let ``given non empty path and None search options, should return a map containing only prefix`` () =
            // Arrange
            let path = Some "/path/to/search"
            let searchOptions = None
            
            let expected = Map<string, obj>["prefix", "/path/to/search"]
            
            // Act
            let result = StorageFileApiHelper.parseSearchOptions path searchOptions
            
            // Assert
            result |> should equal expected

        [<Fact>]
        let ``given search options with values, should return a map with all options set`` () =
            // Arrange
            let path = None
            let sortBy = Some { column = "name"; order = Ascending }
            let searchOptions = Some { limit = Some 50; offset = Some 10; sortBy = sortBy; search = Some "query" }
            
            let expected = Map<string, obj>[
                "prefix", "";
                "limit", 50;
                "offset", 10;
                "sortBy", Map<string, string>["column", "name"; "order", "asc"];
                "search", "query"
            ]
            
            // Act
            let result = StorageFileApiHelper.parseSearchOptions path searchOptions
            
            // Assert
            result |> should equal expected

        [<Fact>]
        let ``given search options with missing values, should return a map with default values set`` () =
            // Arrange
            let path = Some "/path/to/search"
            let sortBy = Some { column = "name"; order = Descending }
            let searchOptions = Some { limit = None; offset = None; sortBy = sortBy; search = None }
            
            let expected = Map<string, obj>[
                "prefix", "/path/to/search";
                "limit", 100;
                "offset", 0;
                "sortBy", Map<string, string>["column", "name"; "order", "desc"];
                "search", ""
            ]
            
            // Act
            let result = StorageFileApiHelper.parseSearchOptions path searchOptions
            
            // Assert
            result |> should equal expected
            
        [<Fact>]
        let ``given search options with missing sortBy, should return a map without sortBy`` () =
            // Arrange
            let path = Some "/path/to/search"
            let searchOptions = Some { limit = Some 10; offset = Some 1; sortBy = None; search = None }
            
            let expected = Map<string, obj>[
                "prefix", "/path/to/search";
                "limit", 10;
                "offset", 1;
                "sortBy", Map<string, string>[];
                "search", ""
            ]
            
            // Act
            let result = StorageFileApiHelper.parseSearchOptions path searchOptions
            
            // Assert
            result |> should equal expected
    
    [<Collection("addTransformValueIfPresent")>]
    module AddTransformValueIfPresentTests =
        [<Fact>]
        let ``should return empty Map if input Map is empty and value is None`` () =
            // Arrange
            let key, value = ("width", None)
            let inputMap = Map<string, string>[]
            let expected = Map<string, string>[]
            
            // Act
            let result = StorageFileApiHelper.addTransformValueIfPresent key value inputMap
            
            // Assert
            result |> should equal expected
            result |> should be Empty
            
        [<Fact>]
        let ``should return input Map if input Map is not empty and value is None`` () =
            // Arrange
            let key, value = ("width", None)
            let inputMap = Map<string, string>["height", "10"]
            let expected = Map<string, string>["height", "10"]
            
            // Act
            let result = StorageFileApiHelper.addTransformValueIfPresent key value inputMap
            
            // Assert
            result |> should equal expected
            
        [<Fact>]
        let ``should return input Map with (key, value) pair added when value is Some`` () =
            // Arrange
            let key, value = ("width", Some 10)
            let inputMap = Map<string, string>["height", "10"]
            let expected = Map<string, string>["height", "10" ; "width", "10"]
            
            // Act
            let result = StorageFileApiHelper.addTransformValueIfPresent key value inputMap
            
            // Assert
            result |> should equal expected
            
    [<Collection("getTransformOptionsParamsMap")>]
    module GetTransformOptionsParamsMapTests =
        [<Fact>]
        let ``should return empty Map when transform is None`` () =
            // Arrange
            let transform = None
            let expected = Map<string, string>[]
            
            // Act
            let result = StorageFileApiHelper.getTransformOptionsParamsMap transform
            
            // Assert
            result |> should equal expected
            result |> should be Empty
            
        [<Fact>]
        let ``should return not empty Map when transform is Some`` () =
            // Arrange
            let transform = 
                { height  = Some 10<pixel>
                  width   = None
                  resize  = None
                  format  = None
                  quality = None }
                
            let expected = Map<string, string>["height", "10"]
            
            // Act
            let result = StorageFileApiHelper.getTransformOptionsParamsMap (Some transform)
            
            // Assert
            result |> should equal expected
            
        [<Fact>]
        let ``should return not Map with all keys when transform has all values`` () =
            // Arrange
            let transform = 
                { height  = Some 10<pixel>
                  width   = Some 10<pixel>
                  resize  = Some FILL
                  format  = Some "f"
                  quality = Some 50<percent> }
                
            let expected = Map<string, string>[
                "height", "10"
                "width", "10"
                "resize", "fill"
                "format", "f"
                "quality", "50"
            ]
            
            // Act
            let result = StorageFileApiHelper.getTransformOptionsParamsMap (Some transform)
            
            // Assert
            result |> should equal expected
            
    [<Collection("transformOptionsToUrlParams")>]
    module TransformOptionsToUrlParams =
        [<Fact>]
        let ``should return empty string when transform is None`` () =
            // Arrange
            let transform = None
            let expected = ""
            
            // Act
            let result = StorageFileApiHelper.transformOptionsToUrlParams transform
            
            // Assert
            result |> should equal expected
            result |> should be Empty
            
        [<Fact>]
        let ``should return non empty valid url params string when transform is Some and has more values`` () =
            // Arrange
            let transform = 
                { height  = Some 10<pixel>
                  width   = None
                  resize  = None
                  format  = None
                  quality = None }
            let expected = "?height=10"
            
            // Act
            let result = StorageFileApiHelper.transformOptionsToUrlParams (Some transform)
            
            // Assert
            result |> should equal expected
        
        [<Fact>]
        let ``should return non empty valid url params string when transform is Some and has one value`` () =
            // Arrange
            let transform = 
                { height  = Some 10<pixel>
                  width   = Some 10<pixel>
                  resize  = Some FILL
                  format  = Some "f"
                  quality = Some 50<percent> }
            let expected = "?format=f&height=10&quality=50&resize=fill&width=10"
            
            // Act
            let result = StorageFileApiHelper.transformOptionsToUrlParams (Some transform)
            
            // Assert
            result |> should equal expected
            
    [<Collection("getFullFilePath")>]
    module GetFullFilePath =
        [<Fact>]
        let ``should return path {bucketId}/{path}`` () =
            // Arrange
            let bucketId = "bucket-1"
            let path = "path"
            let expected = "bucket-1/path"
            
            // Act
            let result = StorageFileApiHelper.getFullFilePath bucketId path
            
            // Assert
            result |> should equal expected
            
    [<Collection("moveOrCopy")>]
    module MoveOrCopyTests =
        [<Fact>]
        let ``should fail when action is not "move" or "copy"`` () =
            // Arrange
            let connection = storageConnection {
                url "http://example.com"
                headers Map["apiKey", "exampleApiKey"]
            }
            let storageFile =
                { connection = connection
                  bucketId = "test-bucket-1"
                  headers = None }
                
            let expectedError =
                { message = "Unsupported action randomAction, use move/copy action!"
                  statusCode = HttpStatusCode.BadRequest }

            // Act
            let result = storageFile |> StorageFileApiHelper.moveOrCopy "from" "to" "randomAction"

            // Assert
            result |> should equal (Error expectedError)

module StorageFileApiTests =
    [<Collection("list")>]
    module ListFilesTests =
        [<Fact>]
        let ``should return a list of files in given bucket without path and searchOptions`` () =
            // Arrange
            let expectedResponse = [
                {
                    id = Some "test-file-1"
                    name = "test-file-1"
                    bucketId = Some "test-bucket-1"
                    owner = Some "test-owner-1"
                    createdAt = Some (DateTime(2023, 1, 1, 12, 0 ,0))
                    updatedAt = Some (DateTime(2023, 1, 1, 12, 0 ,0))
                    lastAccessedAt = None
                    metadata = None
                    bucket = None
                }
                {
                    id = Some "test-file-2"
                    name = "test-file-2"
                    bucketId = Some "test-bucket-1"
                    owner = Some "test-owner-1"
                    createdAt = Some (DateTime(2023, 1, 1, 12, 0 ,0))
                    updatedAt = Some (DateTime(2023, 1, 1, 12, 0 ,0))
                    lastAccessedAt = None
                    metadata = None
                    bucket = None
                }
            ]
            let response =
                """[
                    {
                        "id": "test-file-1",
                        "name": "test-file-1",
                        "bucket_id": "test-bucket-1",
                        "owner": "test-owner-1",
                        "created_at": "2023-01-01T12:00:00Z",
                        "updated_at": "2023-01-01T12:00:00Z"
                    },
                    {
                        "id": "test-file-2",
                        "name": "test-file-2",
                        "bucket_id": "test-bucket-1",
                        "owner": "test-owner-1",
                        "created_at": "2023-01-01T12:00:00Z",
                        "updated_at": "2023-01-01T12:00:00Z"
                    }
                ]"""
                
            let mockHandler = mockHttpMessageHandler response
            let mockHttpClient = new HttpClient(mockHandler.Object)
            
            let connection = storageConnection {
                url "http://example.com"
                headers Map["apiKey", "exampleApiKey"]
                httpClient mockHttpClient
            }
            let storageFile =
                { connection = connection
                  bucketId = "test-bucket-1"
                  headers = None }

            // Act
            let result = storageFile |> StorageFileApi.list None None 

            // Assert
            match result with
            | Ok buckets -> buckets |> should equal expectedResponse
            | Error err -> failwithf $"Expected Ok, but got Error: {err}"
            
            // Verify
            mockHandler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            mockHandler.Protected()
                .Verify("SendAsync", Times.Once(), 
                    ItExpr.Is<HttpRequestMessage>(fun req ->
                        req.Method = HttpMethod.Post &&
                        req.Headers.Contains("apiKey") &&
                        req.RequestUri.AbsoluteUri = "http://example.com/object/list/test-bucket-1"),
                    ItExpr.IsAny<CancellationToken>()
                )
                
        [<Fact>]
        let ``list should return an error when the API request fails`` () =
            // Arrange
            let expectedError = { message = "Bad Request"; statusCode = HttpStatusCode.BadRequest }
                
            let mockHandler = mockHttpMessageHandlerFail expectedError
            let mockHttpClient = new HttpClient(mockHandler.Object)
            
            let connection = storageConnection {
                url "http://example.com"
                headers Map["apiKey", "exampleApiKey"]
                httpClient mockHttpClient
            }
            let storageFile =
                { connection = connection
                  bucketId = "test-bucket-1"
                  headers = None }

            // Act
            let result = storageFile |> StorageFileApi.list None None 

            // Assert
            match result with
            | Ok _ -> failwithf "Expected Error, but got Ok"
            | Error err -> err |> should equal expectedError
            
            // Verify
            mockHandler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            mockHandler.Protected()
                .Verify("SendAsync", Times.Once(), 
                    ItExpr.Is<HttpRequestMessage>(fun req ->
                        req.Method = HttpMethod.Post &&
                        req.Headers.Contains("apiKey") &&
                        req.RequestUri.AbsoluteUri = "http://example.com/object/list/test-bucket-1"),
                    ItExpr.IsAny<CancellationToken>()
                )
                
    [<Collection("move")>]
    module MoveTests =
        [<Fact>]
        let ``should return a success MessageResponse when performed successfully`` () =
            // Arrange
            let expectedResponse = { message = "successfully moved file" }
            let response = """{"message":"successfully moved file"}"""
            let requestBody =
                """{
                    "bucketId":"test-bucket",
                    "sourceKey":"from",
                    "destinationKey":"to"
                }"""
                
            let mockHandler = mockHttpMessageHandlerWithBody response requestBody
            let mockHttpClient = new HttpClient(mockHandler.Object)
            
            let connection = storageConnection {
                url "http://example.com"
                headers Map["apiKey", "exampleApiKey"]
                httpClient mockHttpClient
            }
            let storageFile =
                { connection = connection
                  bucketId = "test-bucket"
                  headers = None }

            // Act
            let result = storageFile |> StorageFileApi.move "from" "to" 

            // Assert
            match result with
            | Ok moveResult -> moveResult |> should equal expectedResponse
            | Error err -> failwithf $"Expected Ok, but got Error: {err}"
            
            // Verify
            mockHandler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            mockHandler.Protected()
                .Verify("SendAsync", Times.Once(), 
                    ItExpr.Is<HttpRequestMessage>(fun req ->
                        req.Method = HttpMethod.Post &&
                        req.Headers.Contains("apiKey") &&
                        req.RequestUri.AbsoluteUri = "http://example.com/object/move" &&
                        req.Content.ReadAsStringAsync().Result = requestBody),
                    ItExpr.IsAny<CancellationToken>()
                )
                
    [<Collection("copy")>]
    module CopyTests =
        [<Fact>]
        let ``should return a success FileResponse when performed successfully`` () =
            // Arrange
            let expectedResponse = { key = "test-bucket" }
            let response = """{"Key":"test-bucket"}"""
            let requestBody =
                """{
                    "bucketId":"test-bucket",
                    "sourceKey":"from",
                    "destinationKey":"to"
                }"""
                
            let mockHandler = mockHttpMessageHandlerWithBody response requestBody
            let mockHttpClient = new HttpClient(mockHandler.Object)
            
            let connection = storageConnection {
                url "http://example.com"
                headers Map["apiKey", "exampleApiKey"]
                httpClient mockHttpClient
            }
            let storageFile =
                { connection = connection
                  bucketId = "test-bucket-1"
                  headers = None }

            // Act
            let result = storageFile |> StorageFileApi.copy "from" "to" 

            // Assert
            match result with
            | Ok copyResult -> copyResult |> should equal expectedResponse
            | Error err -> failwithf $"Expected Ok, but got Error: {err}"
            
            // Verify
            mockHandler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            mockHandler.Protected()
                .Verify("SendAsync", Times.Once(), 
                    ItExpr.Is<HttpRequestMessage>(fun req ->
                        req.Method = HttpMethod.Post &&
                        req.Headers.Contains("apiKey") &&
                        req.RequestUri.AbsoluteUri = "http://example.com/object/copy" &&
                        req.Content.ReadAsStringAsync().Result = requestBody),
                    ItExpr.IsAny<CancellationToken>()
                )