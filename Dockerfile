# GRSMS web application image (multi-stage: build with the SDK, run on the slim ASP.NET runtime).

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, using only the project files, so this layer is cached until a dependency changes.
COPY src/Grsms.Domain/Grsms.Domain.csproj                 src/Grsms.Domain/
COPY src/Grsms.Application/Grsms.Application.csproj       src/Grsms.Application/
COPY src/Grsms.Infrastructure/Grsms.Infrastructure.csproj src/Grsms.Infrastructure/
COPY src/Grsms.Web/Grsms.Web.csproj                       src/Grsms.Web/
RUN dotnet restore src/Grsms.Web/Grsms.Web.csproj

COPY src/ src/
RUN dotnet publish src/Grsms.Web/Grsms.Web.csproj -c Release -o /app/publish --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Writable folders for uploads, saved emails, data-protection keys and logs, owned by the
# image's non-root "app" user so mounted volumes inherit the right permissions.
USER root
RUN mkdir -p /app/App_Data/uploads /app/App_Data/mail /app/App_Data/keys /app/logs \
    && chown -R app:app /app/App_Data /app/logs
USER app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Grsms.Web.dll"]
