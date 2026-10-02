# Build stage - restore, build and publish with the full SDK.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore against just the csproj first, so Docker's layer cache only
# re-runs restore when the project's dependencies actually change, not
# on every source edit.
COPY ThriveWellness.csproj .
RUN dotnet restore "ThriveWellness.csproj"

COPY . .
RUN dotnet build "ThriveWellness.csproj" -c Release -o /app/build --no-restore
RUN dotnet publish "ThriveWellness.csproj" -c Release -o /app/publish --no-restore

# Runtime stage - just the ASP.NET runtime, not the full SDK.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render assigns the actual port via the PORT environment variable at
# container start, which Program.cs reads - this is a documented default
# for `docker run` without PORT set, not what Render will actually use.
EXPOSE 8080

ENTRYPOINT ["dotnet", "ThriveWellness.dll"]
