using Microsoft.OpenApi;

namespace MiniErp.Api.Swagger;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = configuration["Swagger:Title"] ?? "MiniErp API",
                Version = configuration["Swagger:Version"] ?? "v1",
                Description = "MiniErp HTTP API"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token only. Swagger adds the Bearer prefix automatically."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });

            options.SchemaFilter<EnumSchemaDocumentationFilter>();
            options.OperationFilter<AllowAnonymousOperationFilter>();
            options.OperationFilter<AuthenticationSwaggerDocumentation>();
            options.OperationFilter<BusinessPartnersSwaggerDocumentation>();
            options.OperationFilter<CashboxesSwaggerDocumentation>();
            options.OperationFilter<CashMovementTypesSwaggerDocumentation>();
            options.OperationFilter<CashVouchersSwaggerDocumentation>();
            options.OperationFilter<CompaniesSwaggerDocumentation>();
            options.OperationFilter<ContainersSwaggerDocumentation>();
            options.OperationFilter<CountriesSwaggerDocumentation>();
            options.OperationFilter<DriversSwaggerDocumentation>();
            options.OperationFilter<DriverTripsSwaggerDocumentation>();
            options.OperationFilter<ItemsSwaggerDocumentation>();
            options.OperationFilter<ItemUnitsSwaggerDocumentation>();
            options.OperationFilter<StoresSwaggerDocumentation>();
            options.OperationFilter<StoreContainersSwaggerDocumentation>();
            options.OperationFilter<StockOpeningBalancesSwaggerDocumentation>();
            options.OperationFilter<StockAdjustmentsSwaggerDocumentation>();
            options.OperationFilter<InventoryCountsSwaggerDocumentation>();
            options.OperationFilter<StatementsSwaggerDocumentation>();
            options.OperationFilter<PartnerOpeningBalancesSwaggerDocumentation>();
            options.OperationFilter<InvoicesSwaggerDocumentation>();
            options.OperationFilter<UsersSwaggerDocumentation>();
            options.OperationFilter<EnumRequestOperationDocumentationFilter>();
            options.OperationFilter<UnifiedErrorResponseSwaggerFilter>();
        });

        return services;
    }

    public static WebApplication UseSwaggerDocumentation(
        this WebApplication app)
    {
        if (!app.Configuration.GetValue("Swagger:Enabled", true))
        {
            return app;
        }

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            var title = app.Configuration["Swagger:Title"] ?? "MiniErp API";
            var version = app.Configuration["Swagger:Version"] ?? "v1";

            options.SwaggerEndpoint(
                "/swagger/v1/swagger.json",
                $"{title} {version}");
            options.DocumentTitle = $"{title} documentation";
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnablePersistAuthorization();
        });

        app.MapGet(
            "/",
            () => Results.Redirect("/swagger/index.html"))
            .ExcludeFromDescription();

        return app;
    }
}
