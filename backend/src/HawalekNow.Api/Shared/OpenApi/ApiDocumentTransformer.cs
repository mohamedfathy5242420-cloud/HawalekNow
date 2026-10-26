using HawalekNow.Api.Shared.Diagnostics;
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
            "Only application liveness is implemented. SQL/email readiness is not checked. " +
            "Unmatched routes return 404 and unsupported methods return 405 as application/problem+json " +
            "with type, title, status, code and correlationId; no request path or internal diagnostics.";
        foreach (var path in document.Paths.Values)
        foreach (var operation in path.Operations?.Values.AsEnumerable() ?? [])
        {
            operation.Parameters ??= [];
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
                        Schema = new OpenApiSchema
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
                        }
                    }
                };
            }
        }
        return Task.CompletedTask;
    }
}
