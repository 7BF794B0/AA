#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://*:5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TaskTracker/TaskTracker.csproj", "TaskTracker/"]
COPY ["Contracts/Contracts.csproj", "Contracts/"]
COPY ["EventSchemaRegistry/EventSchemaRegistry.csproj", "EventSchemaRegistry/"]
RUN dotnet restore "./TaskTracker/TaskTracker.csproj"
COPY . .
WORKDIR "/src/TaskTracker"
RUN dotnet build "./TaskTracker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TaskTracker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TaskTracker.dll"]