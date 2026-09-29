# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for layer caching
COPY ["JobPortal.Domain/JobPortal.Domain.csproj", "JobPortal.Domain/"]
COPY ["JobPortal.Application/JobPortal.Application.csproj", "JobPortal.Application/"]
COPY ["JobPortal.Infrastructure/JobPortal.Infrastructure.csproj", "JobPortal.Infrastructure/"]
COPY ["JobPortal.API/JobPortal.API.csproj", "JobPortal.API/"]
COPY ["JobPortal.Tests/JobPortal.Tests.csproj", "JobPortal.Tests/"]
COPY ["Directory.Build.props", "./"]

# Restore dependencies
RUN dotnet restore "JobPortal.API/JobPortal.API.csproj"

# Copy full source tree and publish API
COPY . .
WORKDIR "/src/JobPortal.API"
RUN dotnet publish "JobPortal.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create a non-root group and user for security
RUN adduser --disabled-password --home /app --gecos '' appuser && chown -R appuser /app
USER appuser

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "JobPortal.API.dll"]
