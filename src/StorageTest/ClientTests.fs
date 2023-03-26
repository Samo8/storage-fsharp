﻿module ClientTests

open System
open System.Net
open System.Net.Http
open System.Text
open System.Threading
open System.Threading.Tasks
open FsUnit.Xunit
open Moq
open Moq.Protected
open Xunit
open Common
open Connection
open Storage

// type MockHttpMessageHandler(responseContent: string) =
//     inherit HttpMessageHandler()
//
//     member this.ResponseContent
//         with get() = responseContent
//
//     override this.SendAsync(_, __) =
//         let responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
//         responseMessage.Content <- new StringContent(responseContent)
//         responseMessage.Content.Headers.ContentType <- MediaTypeHeaderValue("application/json")
//
//         let response = Task.FromResult(responseMessage)
//         response
        
let mockHttpMessageHandler (response: string) =
    let mockHandler = Mock<HttpMessageHandler>(MockBehavior.Strict)
    mockHandler.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(
            new HttpResponseMessage(HttpStatusCode.OK, Content = new StringContent(response, Encoding.UTF8, "application/json")))
        .Verifiable()
    mockHandler
    
let mockHttpMessageHandlerWithBody (response: string) (requestBody: string) =
    let mockHandler = Mock<HttpMessageHandler>(MockBehavior.Strict)
    mockHandler.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        .Callback(fun (req: HttpRequestMessage) (_: CancellationToken) -> 
            req.Content <- new StringContent(requestBody, Encoding.UTF8, "application/json")
        )
        .ReturnsAsync(
            new HttpResponseMessage(HttpStatusCode.OK, Content = new StringContent(response, Encoding.UTF8, "application/json")))
        .Verifiable()
    mockHandler
    
let mockHttpMessageHandlerFail (error: StorageError) =
    let mockHandler = Mock<HttpMessageHandler>(MockBehavior.Strict)
    mockHandler.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(
            new HttpResponseMessage(error.statusCode, Content = new StringContent(error.message, Encoding.UTF8, "application/json")))
        .Verifiable()
    mockHandler
    
let mockHttpMessageHandlerWithBodyFail (error: StorageError) (requestBody: string) =
    let mockHandler = Mock<HttpMessageHandler>(MockBehavior.Strict)
    mockHandler.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        .Callback(fun (req: HttpRequestMessage) (_: CancellationToken) -> 
            req.Content <- new StringContent(requestBody, Encoding.UTF8, "application/json")
        )
        .ReturnsAsync(
            new HttpResponseMessage(error.statusCode, Content = new StringContent(error.message, Encoding.UTF8, "application/json")))
        .Verifiable()
    mockHandler

[<Collection("listBuckets tests")>]
module ListBucketsTests =
    [<Fact>]
    let ``listBuckets should return a list of Buckets`` () =
        // Arrange
        let expectedResponse = [
            {
                id = "test-bucket-1"
                name = "test-bucket-1"
                owner = Some "test-owner-1"
                createdAt = DateTime(2023, 1, 1, 12, 0 ,0)
                updatedAt = DateTime(2023, 1, 1, 12, 0 ,0)
                _public = true
            };
            {
                id = "test-bucket-2"
                name = "test-bucket-2"
                owner = Some "test-owner-2"
                createdAt = DateTime(2023, 1, 1, 12, 0 ,0)
                updatedAt = DateTime(2023, 1, 1, 12, 0 ,0)
                _public = false
            }
        ]
        let response =
            """[
                {
                    "id": "test-bucket-1",
                    "name": "test-bucket-1",
                    "owner": "test-owner-1",
                    "created_at": "2023-01-01T12:00:00Z",
                    "updated_at": "2023-01-01T12:00:00Z",
                    "public": true
                },
                {
                    "id": "test-bucket-2",
                    "name": "test-bucket-2",
                    "owner": "test-owner-2",
                    "created_at": "2023-01-01T12:00:00Z",
                    "updated_at": "2023-01-01T12:00:00Z",
                    "public": false
                }
            ]"""
            
        let mockHandler = mockHttpMessageHandler response
        let mockHttpClient = new HttpClient(mockHandler.Object)
        
        let connection = storageConnection {
            url "http://example.com"
            headers Map["apiKey", "exampleApiKey"]
            httpClient mockHttpClient
        }

        // Act
        let result = Client.listBuckets connection

        // Assert
        match result with
        | Ok buckets -> buckets |> should equal expectedResponse
        | Error err -> failwithf $"Expected Ok, but got Error: {err}"
        
        // Verify
        mockHandler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(), 
                ItExpr.Is<HttpRequestMessage>(fun req ->
                    req.Method = HttpMethod.Get &&
                    req.Headers.Contains("apiKey") &&
                    req.RequestUri.AbsoluteUri = "http://example.com/bucket"),
                ItExpr.IsAny<CancellationToken>()
            )

    [<Fact>]
    let ``listBuckets should return an error when the API request fails`` () =
        // Arrange
        let expectedError = { message = "Bad Request"; statusCode = HttpStatusCode.BadRequest }

        let mockHandler = mockHttpMessageHandlerFail expectedError
        let mockHttpClient = new HttpClient(mockHandler.Object)
        
        let connection = storageConnection {
            url "http://example.com"
            headers Map["apiKey", "exampleApiKey"]
            httpClient mockHttpClient
        }

        // Act
        let result = Client.listBuckets connection

        // Assert
        match result with
        | Ok _ -> failwithf "Expected Error, but got Ok"
        | Error err -> err |> should equal expectedError
        
        // Verify
        mockHandler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(), 
                ItExpr.Is<HttpRequestMessage>(fun req ->
                    req.Method = HttpMethod.Get &&
                    req.Headers.Contains("apiKey") &&
                    req.RequestUri.AbsoluteUri = "http://example.com/bucket"),
                ItExpr.IsAny<CancellationToken>()
            )
        
[<Collection("getBucket tests")>]
module GetBucketTests =
    [<Fact>]
    let ``getBucket should successfully return a Bucket`` () =
        // Arrange
        let bucketId = "123"
        let expectedResponse = {
            id = "test-bucket"
            name = "test-bucket"
            owner = Some "test-owner"
            createdAt = DateTime(2023, 1, 1, 12, 0 ,0)
            updatedAt = DateTime(2023, 1, 1, 12, 0 ,0)
            _public = true
        }
        let response =
            """{
                "id": "test-bucket",
                "name": "test-bucket",
                "owner": "test-owner",
                "created_at": "2023-01-01T12:00:00Z",
                "updated_at": "2023-01-01T12:00:00Z",
                "public": true
            }"""
            
        let mockHandler = mockHttpMessageHandler response
        let mockHttpClient = new HttpClient(mockHandler.Object)
        
        let connection = storageConnection {
            url "http://example.com"
            headers Map["apiKey", "exampleApiKey"]
            httpClient mockHttpClient
        }

        // Act
        let result = Client.getBucket bucketId connection

        // Assert
        match result with
        | Ok bucket -> bucket |> should equal expectedResponse
        | Error err -> failwithf $"Expected Ok, but got Error: {err}"
        
        // Verify
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(), 
                ItExpr.Is<HttpRequestMessage>(fun req ->
                    req.Method = HttpMethod.Get &&
                    req.Headers.Contains("apiKey") &&
                    req.RequestUri.AbsoluteUri = $"http://example.com/bucket/{bucketId}"),
                ItExpr.IsAny<CancellationToken>()
            )
            
    [<Fact>]
    let ``getBucket should return an error when the API request fails`` () =
        // Arrange
        let bucketId = "123"
        let expectedError = { message = "Bad Request"; statusCode = HttpStatusCode.BadRequest }
            
        let mockHandler = mockHttpMessageHandlerFail expectedError
        let mockHttpClient = new HttpClient(mockHandler.Object)
        
        let connection = storageConnection {
            url "http://example.com"
            headers Map["apiKey", "exampleApiKey"]
            httpClient mockHttpClient
        }

        // Act
        let result = Client.getBucket bucketId connection

        match result with
        | Ok _ -> failwithf "Expected Error, but got Ok"
        | Error err -> err |> should equal expectedError
        
        // Verify
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(), 
                ItExpr.Is<HttpRequestMessage>(fun req ->
                    req.Method = HttpMethod.Get &&
                    req.Headers.Contains("apiKey") &&
                    req.RequestUri.AbsoluteUri = $"http://example.com/bucket/{bucketId}"),
                ItExpr.IsAny<CancellationToken>()
            )

[<Collection("createBucket tests")>]
module CreateBucketTests =
    [<Fact>]
    let ``createBucket should create a new bucket and return the created bucket`` () =
        // Arrange
        let expectedResponse: CreateBucket = { name = "Bucket test-bucket created successfully" }
        let response =
            """{
                "name": "Bucket test-bucket created successfully"
            }"""
        let requestBody =
            """{
                "id":"test-bucket",
                "name":"test-bucket",
                "public":true
            }"""
        
        let mockHandler = mockHttpMessageHandlerWithBody response requestBody
        let mockHttpClient = new HttpClient(mockHandler.Object)
        let connection = storageConnection {
            url "http://example.com"
            headers Map["apiKey", "exampleApiKey"]
            httpClient mockHttpClient
        }

        // Act
        let result = Client.createBucket "test-bucket" (Some { _public = true }) connection

        // Assert
        match result with
        | Ok bucket -> bucket |> should equal expectedResponse
        | Error err -> failwithf $"Expected Ok, but got Error: {err}"

        // Verify
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.Is<HttpRequestMessage>(fun req ->
                    req.Method = HttpMethod.Post &&
                    req.Headers.Contains("apiKey") &&
                    req.RequestUri.ToString() = "http://example.com/bucket" &&
                    req.Content.ReadAsStringAsync().Result = requestBody),
                ItExpr.IsAny<CancellationToken>())
            
    [<Fact>]
    let ``createBucket should return an error when the API request fails`` () =
        // Arrange
        let expectedError = { message = "Bad Request"; statusCode = HttpStatusCode.BadRequest }
        let requestBody =
            """{
                "id":"test-bucket",
                "name":"test-bucket",
                "public":true
            }"""
        
        let mockHandler = mockHttpMessageHandlerWithBodyFail expectedError requestBody
        let mockHttpClient = new HttpClient(mockHandler.Object)
        let connection = storageConnection {
            url "http://example.com"
            headers Map["apiKey", "exampleApiKey"]
            httpClient mockHttpClient
        }

        // Act
        let result = Client.createBucket "test-bucket" (Some { _public = true }) connection

        // Assert
        match result with
        | Ok _ -> failwithf "Expected Error, but got Ok"
        | Error err -> err |> should equal expectedError

        // Verify
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.Is<HttpRequestMessage>(fun req ->
                    req.Method = HttpMethod.Post &&
                    req.Headers.Contains("apiKey") &&
                    req.RequestUri.ToString() = "http://example.com/bucket" &&
                    req.Content.ReadAsStringAsync().Result = requestBody),
                ItExpr.IsAny<CancellationToken>())

[<Collection("emptyBucket tests")>]
module EmptyBucketTests=
    [<Fact>]
    let ``emptyBucket should successfully empty a bucket with given id`` () =
        // Arrange
        let bucketId = "123"
        let expectedResponse = {
            message =  $"Bucket {bucketId} emptied"
        }
        let response =
            """{
                "message": "Bucket 123 emptied"
            }"""
            
        let mockHandler = mockHttpMessageHandler response
        let mockHttpClient = new HttpClient(mockHandler.Object)
        
        let connection = storageConnection {
            url "http://example.com"
            headers Map["apiKey", "exampleApiKey"]
            httpClient mockHttpClient
        }

        // Act
        let result = Client.emptyBucket bucketId connection

        // Assert
        match result with
        | Ok bucket -> bucket |> should equal expectedResponse
        | Error err -> failwithf $"Expected Ok, but got Error: {err}"
        
        // Verify
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(), 
                ItExpr.Is<HttpRequestMessage>(fun req ->
                    req.Method = HttpMethod.Post &&
                    req.Headers.Contains("apiKey") &&
                    req.RequestUri.AbsoluteUri = $"http://example.com/bucket/{bucketId}/empty"),
                ItExpr.IsAny<CancellationToken>()
            )
            
    [<Fact>]
    let ``emptyBucket should return an error when the API request fails`` () =
        // Arrange
        let bucketId = "123"
        let expectedError = { message = "Bad Request"; statusCode = HttpStatusCode.BadRequest }
            
        let mockHandler = mockHttpMessageHandlerFail expectedError
        let mockHttpClient = new HttpClient(mockHandler.Object)
        
        let connection = storageConnection {
            url "http://example.com"
            headers Map["apiKey", "exampleApiKey"]
            httpClient mockHttpClient
        }

        // Act
        let result = Client.emptyBucket bucketId connection

        // Assert
        match result with
        | Ok _ -> failwithf "Expected Error, but got Ok"
        | Error err -> err |> should equal expectedError
        
        // Verify
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(), 
                ItExpr.Is<HttpRequestMessage>(fun req ->
                    req.Method = HttpMethod.Post &&
                    req.Headers.Contains("apiKey") &&
                    req.RequestUri.AbsoluteUri = $"http://example.com/bucket/{bucketId}/empty"),
                ItExpr.IsAny<CancellationToken>()
            )
        
[<Collection("deleteBucket tests")>]
module DeleteBucketTests =
    [<Fact>]
    let ``deleteBucket should successfully delete a bucket with given id`` () =
        // Arrange
        let bucketId = "123"
        let expectedResponse = {
            message =  $"Bucket {bucketId} deleted"
        }
        let response =
            """{
                "message": "Bucket 123 deleted"
            }"""
            
        let mockHandler = mockHttpMessageHandler response
        let mockHttpClient = new HttpClient(mockHandler.Object)
        
        let connection = storageConnection {
            url "http://example.com"
            headers Map["apiKey", "exampleApiKey"]
            httpClient mockHttpClient
        }

        // Act
        let result = Client.deleteBucket bucketId connection

        // Assert
        match result with
        | Ok bucket -> bucket |> should equal expectedResponse
        | Error err -> failwithf $"Expected Ok, but got Error: {err}"
        
        // Verify
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.Is<HttpRequestMessage>(fun req ->
                    req.Method = HttpMethod.Delete &&
                    req.Headers.Contains("apiKey") &&
                    req.RequestUri.AbsoluteUri = $"http://example.com/bucket/{bucketId}"),
                ItExpr.IsAny<CancellationToken>()
            )
            
    [<Fact>]
    let ``deleteBucket should return an error when the API request fails`` () =
        // Arrange
        let bucketId = "123"
        let expectedError = { message = "Bad Request"; statusCode = HttpStatusCode.BadRequest }
            
        let mockHandler = mockHttpMessageHandlerFail expectedError
        let mockHttpClient = new HttpClient(mockHandler.Object)
        
        let connection = storageConnection {
            url "http://example.com"
            headers Map["apiKey", "exampleApiKey"]
            httpClient mockHttpClient
        }

        // Act
        let result = Client.deleteBucket bucketId connection

        // Assert
        match result with
        | Ok _ -> failwithf "Expected Error, but got Ok"
        | Error err -> err |> should equal expectedError
        
        // Verify
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.Is<HttpRequestMessage>(fun req ->
                    req.Method = HttpMethod.Delete &&
                    req.Headers.Contains("apiKey") &&
                    req.RequestUri.AbsoluteUri = $"http://example.com/bucket/{bucketId}"),
                ItExpr.IsAny<CancellationToken>()
            )

[<Collection("from tests")>]
module FromTests =
    [<Fact>]
    let ``from should return StorageFile with given StorageConnection and bucketId`` () =
        // Arrange
        let bucketId = "123"
        let connection = storageConnection {
            url "http://example.com"
            headers Map["apiKey", "exampleApiKey"]
        }

        // Act
        let storageFile = Client.from bucketId connection

        // Assert
        storageFile.connection |> should equal connection
        storageFile.bucketId |> should equal bucketId
        storageFile.headers |> should equal None