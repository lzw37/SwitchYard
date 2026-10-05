using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SwitchYard.StationLayout;

public static class StationLayoutEndpointExtensions
{
    public const string DefaultRoutePrefix = "/StationLayout";
    public const long DefaultMaximumDwgFileSize = 20L * 1024 * 1024;

    public static IEndpointRouteBuilder MapSwitchYardStationLayout(
        this IEndpointRouteBuilder endpoints,
        string routePrefix = DefaultRoutePrefix,
        long maximumDwgFileSize = DefaultMaximumDwgFileSize)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (string.IsNullOrWhiteSpace(routePrefix))
        {
            throw new ArgumentException("The station-layout route prefix cannot be empty.", nameof(routePrefix));
        }

        if (maximumDwgFileSize <= 0 || maximumDwgFileSize > DefaultMaximumDwgFileSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDwgFileSize),
                "The maximum DWG file size must be between one byte and 20 MB.");
        }

        // The module requires an authenticated principal but deliberately does not
        // select a concrete authentication scheme. JWT/cookie/custom auth remains
        // a host concern.
        var group = endpoints.MapGroup(routePrefix.TrimEnd('/'))
            .RequireAuthorization();

        group.MapGet("/GetStationSchemes", (HttpContext context, IStationLayoutService service) =>
            ExecuteAsync(context, async cancellationToken =>
            {
                var result = await service.GetStationSchemesAsync(
                    context.User,
                    Query(context.Request, "instanceID"),
                    cancellationToken);
                return Results.Json(result);
            }));

        group.MapPost("/CreateStationScheme", (HttpContext context, IStationLayoutService service) =>
            ExecuteAsync(context, async cancellationToken =>
            {
                var request = await ReadJsonAsync<StationSchemeCreateRequest>(context, cancellationToken);
                return Results.Json(await service.CreateStationSchemeAsync(
                    context.User,
                    request,
                    cancellationToken));
            }));

        group.MapPost("/CopyStationScheme", (HttpContext context, IStationLayoutService service) =>
            ExecuteAsync(context, async cancellationToken =>
            {
                var request = await ReadJsonAsync<StationSchemeCopyRequest>(context, cancellationToken);
                return Results.Json(await service.CopyStationSchemeAsync(
                    context.User,
                    request,
                    cancellationToken));
            }));

        group.MapPut("/EditStationScheme", (HttpContext context, IStationLayoutService service) =>
            ExecuteAsync(context, async cancellationToken =>
            {
                var request = await ReadJsonAsync<StationSchemeUpdateRequest>(context, cancellationToken);
                return Results.Json(await service.EditStationSchemeAsync(
                    context.User,
                    request,
                    cancellationToken));
            }));

        group.MapDelete("/DeleteStationScheme", (HttpContext context, IStationLayoutService service) =>
            ExecuteAsync(context, async cancellationToken =>
            {
                await service.DeleteStationSchemeAsync(
                    context.User,
                    Query(context.Request, "instanceID"),
                    Query(context.Request, "stationSchemeID"),
                    cancellationToken);
                return Results.Text("Station scheme deleted successfully.");
            }));

        group.MapPost("/GetJson", (HttpContext context, IStationLayoutService service) =>
            ExecuteAsync(context, async cancellationToken =>
            {
                var result = await service.GetJsonAsync(
                    context.User,
                    Query(context.Request, "instanceID"),
                    OptionalQuery(context.Request, "stationSchemeID"),
                    cancellationToken);
                SetRevisionHeaders(context.Response, result.Revision);
                context.Response.Headers["X-Station-Layout-Exists"] = result.Exists ? "true" : "false";
                return Results.Text(result.Document.ToJson(), "application/json");
            }));

        group.MapPost("/SaveJson", (HttpContext context, IStationLayoutService service) =>
            ExecuteAsync(context, async cancellationToken =>
            {
                var request = await ReadJsonAsync<StationLayoutSaveRequest>(context, cancellationToken);
                request.InstanceID = PreferQuery(context.Request, "instanceID", request.InstanceID);
                request.StationSchemeID = PreferQuery(
                    context.Request,
                    "stationSchemeID",
                    request.StationSchemeID);
                request.ExpectedRevision = ParseExpectedRevision(
                    context.Request,
                    request.ExpectedRevision);
                var result = await service.SaveJsonAsync(context.User, request, cancellationToken);
                SetRevisionHeaders(context.Response, result.Revision);
                return Results.Json(result);
            }));

        group.MapPost("/SearchRoutes", (HttpContext context, IStationLayoutService service) =>
            ExecuteAsync(context, async cancellationToken =>
            {
                var request = await ReadJsonAsync<StationRouteSearchRequest>(context, cancellationToken);
                request.InstanceID = PreferQuery(context.Request, "instanceID", request.InstanceID);
                request.StationSchemeID = PreferQuery(
                    context.Request,
                    "stationSchemeID",
                    request.StationSchemeID);
                return Results.Json(await service.SearchRoutesAsync(
                    context.User,
                    request,
                    cancellationToken));
            }));

        group.MapPost("/ExtractDwgFile", (HttpContext context, IStationLayoutService service) =>
                ExecuteAsync(context, async cancellationToken =>
                {
                    if (!context.Request.HasFormContentType)
                    {
                        throw new StationLayoutValidationException(
                            "A multipart/form-data request is required.");
                    }

                    var form = await context.Request.ReadFormAsync(cancellationToken);
                    var file = form.Files.GetFile("file")
                               ?? throw new StationLayoutValidationException(
                                   "The DWG file field is required.");
                    if (file.Length == 0)
                    {
                        throw new StationLayoutValidationException(
                            "Please upload a non-empty DWG file.");
                    }

                    if (file.Length > maximumDwgFileSize)
                    {
                        throw new StationLayoutValidationException(
                            $"File size exceeds the {maximumDwgFileSize / (1024 * 1024)} MB limit.");
                    }

                    await using var stream = file.OpenReadStream();
                    var result = await service.ExtractDwgAsync(
                        context.User,
                        stream,
                        file.FileName,
                        form["layerName"].ToString(),
                        cancellationToken);
                    return Results.Json(result);
                }))
            .DisableAntiforgery()
            .WithMetadata(
                new RequestSizeLimitAttribute(maximumDwgFileSize),
                new RequestFormLimitsAttribute
                {
                    MultipartBodyLengthLimit = maximumDwgFileSize
                });

        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync(
        HttpContext context,
        Func<CancellationToken, Task<IResult>> action)
    {
        try
        {
            return await action(context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (StationLayoutValidationException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (JsonException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (BadHttpRequestException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (InvalidDataException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (StationLayoutUnauthenticatedException exception)
        {
            return Error(StatusCodes.Status401Unauthorized, exception.Message);
        }
        catch (StationLayoutForbiddenException exception)
        {
            return Error(StatusCodes.Status403Forbidden, exception.Message);
        }
        catch (StationLayoutNotFoundException exception)
        {
            return Error(StatusCodes.Status404NotFound, exception.Message);
        }
        catch (StationLayoutConflictException exception)
        {
            return Error(StatusCodes.Status409Conflict, exception.Message);
        }
        catch (StationLayoutStoreException)
        {
            return Error(
                StatusCodes.Status500InternalServerError,
                "The station-layout store failed to process the request.");
        }
        catch (Exception)
        {
            return Error(
                StatusCodes.Status500InternalServerError,
                "The station-layout request failed.");
        }
    }

    private static async Task<T> ReadJsonAsync<T>(
        HttpContext context,
        CancellationToken cancellationToken)
        where T : class
    {
        return await context.Request.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
               ?? throw new StationLayoutValidationException("A JSON request body is required.");
    }

    private static long? ParseExpectedRevision(HttpRequest request, long? bodyValue)
    {
        if (bodyValue is < 0)
        {
            throw new StationLayoutValidationException("expectedRevision cannot be negative.");
        }

        if (bodyValue.HasValue)
        {
            return bodyValue;
        }

        var value = request.Headers.IfMatch.ToString().Trim();
        if (value.Length == 0)
        {
            return null;
        }

        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            throw new StationLayoutValidationException(
                "If-Match must contain a strong numeric station-layout revision.");
        }

        value = value.Trim('"');
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var revision) ||
            revision < 0)
        {
            throw new StationLayoutValidationException(
                "If-Match must contain a numeric station-layout revision.");
        }

        return revision;
    }

    private static void SetRevisionHeaders(HttpResponse response, long revision)
    {
        response.Headers.ETag = $"\"{revision.ToString(CultureInfo.InvariantCulture)}\"";
        response.Headers["X-Station-Layout-Revision"] =
            revision.ToString(CultureInfo.InvariantCulture);
    }

    private static IResult Error(int statusCode, string message) =>
        Results.Text(message, "text/plain; charset=utf-8", statusCode: statusCode);

    private static string Query(HttpRequest request, string key) =>
        request.Query[key].ToString();

    private static string? OptionalQuery(HttpRequest request, string key)
    {
        var value = request.Query[key].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? PreferQuery(HttpRequest request, string key, string? bodyValue) =>
        OptionalQuery(request, key) ?? bodyValue;
}
