#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["Alembic/Alembic.csproj", "Alembic/"]
RUN dotnet restore "Alembic/Alembic.csproj"
COPY . .
WORKDIR "/src/Alembic"
RUN dotnet build "Alembic.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Alembic.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Alembic.dll"]
