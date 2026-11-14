using HawalekNow.Api.Shared.Diagnostics;
using HawalekNow.Api.Shared.Security;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace HawalekNow.Api.Shared.OpenApi;

public sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "HawalekNow API";
        document.Info.Version = "v1";
        document.Info.Description = "api/v1 is a fixed controller route prefix, not request version negotiation. " +
            "Identity registration, email confirmation, password reset, sessions and account recovery use HTTPS cookies, CSRF protection and per-peer request quotas. " +
            "Fetch identity/csrf with the current cookie jar before every mutation. Access/refresh tokens are never returned in response bodies. " +
            "Customer and Merchant accounts are separate; specify accountType for login and recovery. Confirmed Pending merchants can prepare their account but cannot publish. " +
            "Self-delete requires password reauthentication and is forbidden for Admin. AccountRecovery OTP restores before the exact30-day deadline without reviving sessions. " +
            "SQL/email readiness is not checked. " +
            "Unmatched routes return 404 and unsupported methods return 405 as application/problem+json " +
            "with type, title, status, code and correlationId; no request path or internal diagnostics.";
        foreach (var path in document.Paths.Values)
            foreach (var entry in path.Operations ?? [])
            {
                var operation = entry.Value;
                operation.Parameters ??= [];
                if (entry.Key == HttpMethod.Post || entry.Key == HttpMethod.Put || entry.Key == HttpMethod.Patch || entry.Key == HttpMethod.Delete)
                {
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = BrowserSecurity.CsrfHeader,
                        In = ParameterLocation.Header,
                        Required = true,
                        Description = "Fetch identity/csrf over HTTPS using the current cookies and send its requestToken here. Refresh it after changing principal; this is not an access/refresh token.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1 }
                    });
                    operation.Responses ??= new OpenApiResponses();
                    foreach (var (status, description) in new[] { ("400", "Invalid CSRF token or request input."), ("403", "HTTPS is required or access is forbidden."), ("429", "Request quota exceeded; honor Retry-After.") })
                        operation.Responses.TryAdd(status, new OpenApiResponse
                        {
                            Description = description,
                            Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() { Schema = SafeProblemSchema() } }
                        });
                }
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = CorrelationIdMiddleware.HeaderName,
                    In = ParameterLocation.Header,
                    Required = false,
                    Description = "Optional untrusted correlation metadata. Invalid or multiple values are replaced with a generated ID.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = 64, Pattern = "^[A-Za-z0-9_-]+$" }
                });
                foreach (var response in operation.Responses?.Values.OfType<OpenApiResponse>() ?? [])
                {
                    response.Headers ??= new Dictionary<string, IOpenApiHeader>();
                    response.Headers[CorrelationIdMiddleware.HeaderName] = new OpenApiHeader
                    {
                        Description = "The effective correlation ID for this response.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = 64, Pattern = "^[A-Za-z0-9_-]+$" }
                    };
                }
                if (operation.Responses?.TryGetValue("500", out var error) == true && error is OpenApiResponse errorResponse)
                {
                    errorResponse.Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/problem+json"] = new()
                        {
                            Schema = SafeProblemSchema()
                        }
                    };
                }
            }
        return Task.CompletedTask;
    }
    private static OpenApiSchema SafeProblemSchema() => new()
    {
        Type = JsonSchemaType.Object,
        AdditionalPropertiesAllowed = false,
        Required = new HashSet<string> { "type", "title", "status", "code", "correlationId" },
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["type"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
            ["code"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["correlationId"] = new OpenApiSchema { Type = JsonSchemaType.String }
        }
    };
}
