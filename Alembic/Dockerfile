FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
ADD ./Alembic/VERSION .
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["Alembic/Alembic.csproj", "Alembic/"]
COPY ["Alembic.Docker.Api/Alembic.Docker.Api.csproj", "Alembic.Docker.Api/"]
COPY ["Alembic.Docker/Alembic.Docker.csproj", "Alembic.Docker/"]
COPY ["Alembic.Reporting/Alembic.Reporting.csproj", "Alembic.Reporting/"]
COPY ["Alembic.Common/Alembic.Common.csproj", "Alembic.Common/"]

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