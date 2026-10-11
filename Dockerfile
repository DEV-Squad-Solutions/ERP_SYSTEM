# =============================================================
#  MiniErp API — صورة نشر الإنتاج (.NET 10 / Clean Architecture)
#  متعدد المراحل: بناء (SDK) ثم تشغيل خفيف (ASP.NET Runtime)
# =============================================================

# ---------- مرحلة البناء ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# خصائص البناء على مستوى الحل (تحدد TargetFramework = net10.0)
COPY Directory.Build.props ./

# المشاريع الأربع: Api + Application + Infrastructure + Domain
COPY src/ ./src/

RUN dotnet restore src/MiniErp.Api/MiniErp.Api.csproj

RUN dotnet publish src/MiniErp.Api/MiniErp.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- مرحلة التشغيل ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl لفحص الصحة (healthcheck) داخل الحاوية
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "MiniErp.Api.dll"]