module GiraffeBackendFor_Angular_GettingStarted.App

open System
open System.IO
open System.Text
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.SpaServices
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks
open Giraffe
open Giraffe.EndpointRouting
open Newtonsoft.Json
open Newtonsoft.Json.Converters

// ---------------------------------
// Models
// ---------------------------------

type DateTimeConverter() =
    inherit IsoDateTimeConverter(DateTimeFormat = "MMMM dd, yyyy")

type Product =
    {
        ProductId   : int
        ProductName : string
        ProductCode : string
        [<JsonConverter(typeof<DateTimeConverter>)>]
        ReleaseDate : DateTimeOffset
        Description : string
        Price       : decimal
        StarRating  : float
        ImageUrl    : string
    }

let sourcePath = Path.Combine("ClientApp", "APM-Final")
let contentPath = Path.Combine(sourcePath, "src")
let spaHref = "http://localhost:4200"

// ---------------------------------
// Web app
// ---------------------------------

let productsHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let filePath = Path.Combine(contentPath, "api", "products", "products.json")
            let text = File.ReadAllText(filePath, Encoding.UTF8)
            let serializer = ctx.GetJsonSerializer()
            let result = serializer.Deserialize<Product list> text
            return! json result next ctx
        }

let endpoints = [
    subRoute "/api" [
        route "/products" productsHandler ]]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins(
            spaHref,
            "http://localhost:5000",
            "https://localhost:5001")
       .AllowAnyMethod()
       .AllowAnyHeader()
       |> ignore

let configureSpa (spa : ISpaBuilder) =
    spa.Options.SourcePath <- sourcePath
    spa.UseProxyToSpaDevelopmentServer spaHref

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app.UseGiraffeErrorHandler(errorHandler)
           .UseHsts())
        .UseHttpsRedirection()
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseSpaStaticFiles()
    app.UseRouting()
       .UseEndpoints(fun e -> e.MapGiraffeEndpoints(endpoints))
       .UseSpa(Action<ISpaBuilder> configureSpa)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore
    services.AddSpaStaticFiles(fun config -> config.RootPath <- contentPath)

let configureLogging (builder : ILoggingBuilder) =
    builder.AddFilter(fun (name:string) (l:LogLevel) -> name = "Default" && l = LogLevel.Information)
           .AddFilter(fun (name:string) (l:LogLevel) -> name = "Microsoft" && l = LogLevel.Warning)
           .AddFilter(fun (name:string) (l:LogLevel) -> name = "Microsoft.Hosting.Lifetime" && l = LogLevel.Information)
           .AddConsole()
           .AddDebug()
           |> ignore

[<EntryPoint>]
let main args =
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0
