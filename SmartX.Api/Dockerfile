# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5050
ENV ASPNETCORE_URLS=http://+:5050

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SmartX.Api/SmartX.Api.csproj", "SmartX.Api/"]
RUN dotnet restore "SmartX.Api/SmartX.Api.csproj"
COPY . .
WORKDIR "/src/SmartX.Api"
RUN dotnet build "SmartX.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "SmartX.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final production stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartX.Api.dll"]
