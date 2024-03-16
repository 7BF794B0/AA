#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 5003
ENV ASPNETCORE_URLS=http://*:5003

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Analytics/Analytics.csproj", "Analytics/"]
COPY ["Contracts/Contracts.csproj", "Contracts/"]
COPY ["EventSchemaRegistry/EventSchemaRegistry.csproj", "EventSchemaRegistry/"]
RUN dotnet restore "./Analytics/Analytics.csproj"
COPY . .
WORKDIR "/src/Analytics"
RUN dotnet build "./Analytics.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Analytics.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Analytics.dll"]