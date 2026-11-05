# Build context: the repository root (docker build -f DocMind.Api/Dockerfile .),
# since DocMind.Api references DocMind.Core.

# ---- build: publish the API and the migrations bundle ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Project files first: this restore layer stays cached until a dependency changes.
COPY .config/dotnet-tools.json .config/
COPY DocMind.Api/DocMind.Api.csproj DocMind.Api/
COPY DocMind.Core/DocMind.Core.csproj DocMind.Core/
RUN dotnet tool restore && dotnet restore DocMind.Api/DocMind.Api.csproj

COPY .editorconfig ./
COPY DocMind.Core/ DocMind.Core/
COPY DocMind.Api/ DocMind.Api/
RUN dotnet publish DocMind.Api/DocMind.Api.csproj -c Release -o /out/api --no-restore

# dotnet ef starts Program.cs to find DocMindDbContext, and Program.cs throws without a
# connection string. This placeholder is never used to connect; the bundle reads the real
# one from ConnectionStrings__DocMind when it runs.
ENV ConnectionStrings__DocMind="Host=placeholder;Database=placeholder"
RUN dotnet ef migrations bundle --project DocMind.Core --startup-project DocMind.Api \
    --configuration Release --output /out/efbundle

# ---- migrate: applies pending migrations, then exits ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS migrate
WORKDIR /app
COPY --from=build /out/efbundle .
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# ---- api (last, so a build without --target produces the API) ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
WORKDIR /app
COPY --from=build /out/api .
# Data Protection keys go here. Creating it owned by the app user means the named volume
# mounted on it starts out writable by that user.
RUN mkdir /keys && chown $APP_UID /keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DocMind.Api.dll"]
