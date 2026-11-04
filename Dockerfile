# syntax=docker/dockerfile:1
# Builds either host: docker build --build-arg PROJECT=RentalHive.Web (default) or PROJECT=RentalHive.Api
ARG PROJECT=RentalHive.Web

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY RentalHive.Domain/RentalHive.Domain.csproj RentalHive.Domain/
COPY RentalHive.Application/RentalHive.Application.csproj RentalHive.Application/
COPY RentalHive.Infrastructure/RentalHive.Infrastructure.csproj RentalHive.Infrastructure/
COPY RentalHive.Api/RentalHive.Api.csproj RentalHive.Api/
COPY RentalHive.Web/RentalHive.Web.csproj RentalHive.Web/
RUN dotnet restore ${PROJECT}/${PROJECT}.csproj
COPY RentalHive.Domain/ RentalHive.Domain/
COPY RentalHive.Application/ RentalHive.Application/
COPY RentalHive.Infrastructure/ RentalHive.Infrastructure/
COPY RentalHive.Api/ RentalHive.Api/
COPY RentalHive.Web/ RentalHive.Web/
RUN dotnet publish ${PROJECT}/${PROJECT}.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG PROJECT
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    APP_DLL=${PROJECT}.dll \
    Storage__ImageDirectory=/data/images \
    DataProtection__KeysPath=/data/keys
WORKDIR /app
COPY --from=build /app ./
RUN mkdir -p /data/images /data/keys && chown -R app:app /data
# The official .NET images ship a non-root "app" user.
USER app
EXPOSE 8080
VOLUME ["/data"]
# Probe GET /health from your orchestrator (it checks the database connection).
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
