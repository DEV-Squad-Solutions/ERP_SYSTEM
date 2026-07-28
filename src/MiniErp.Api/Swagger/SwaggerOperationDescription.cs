namespace MiniErp.Api.Swagger;

internal static class SwaggerOperationDescription
{
    public static string Create(
        string overview,
        string requiredFields,
        string validation,
        string edgeCases) =>
        $"""
        {overview}

        **Required fields:** {requiredFields}

        **Validation:** {validation}

        **Edge cases:** {edgeCases}
        """;
}
