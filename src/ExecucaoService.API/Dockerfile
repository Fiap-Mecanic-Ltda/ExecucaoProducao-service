# Imagem do Execução Service. Mesmo modelo do MechanicLtda e do OS Service (agente New Relic
# instalado e desligado por padrão), sem as dependências nativas do QuestPDF.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER root

# curl: health check do docker-compose.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Agente APM do New Relic para .NET. Fica desligado (CORECLR_ENABLE_PROFILING=0); quem liga é o
# deployment do cluster, junto com NEW_RELIC_LICENSE_KEY e NEW_RELIC_APP_NAME.
RUN apt-get update && apt-get install -y --no-install-recommends wget ca-certificates gnupg \
    && echo 'deb [signed-by=/usr/share/keyrings/newrelic-apt.gpg] http://apt.newrelic.com/debian/ newrelic non-free' \
       > /etc/apt/sources.list.d/newrelic.list \
    && wget -qO- https://download.newrelic.com/NEWRELIC_APT_2DAD550E.public \
       | gpg --import --batch --no-default-keyring --keyring /usr/share/keyrings/newrelic-apt.gpg \
    && apt-get update && apt-get install -y --no-install-recommends newrelic-dotnet-agent \
    && apt-get purge -y --auto-remove wget gnupg \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /usr/local/newrelic-dotnet-agent/logs \
    && chown -R $APP_UID /usr/local/newrelic-dotnet-agent/logs

ENV CORECLR_ENABLE_PROFILING=0 \
    CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
    CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
    CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so

USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Só os arquivos de projeto primeiro, para aproveitar o cache do restore.
COPY ["Directory.Build.props", "./"]
COPY ["src/ExecucaoService.API/ExecucaoService.API.csproj", "src/ExecucaoService.API/"]
COPY ["src/ExecucaoService.Bootstrap/ExecucaoService.Bootstrap.csproj", "src/ExecucaoService.Bootstrap/"]
COPY ["src/ExecucaoService.Application/ExecucaoService.Application.csproj", "src/ExecucaoService.Application/"]
COPY ["src/ExecucaoService.Domain/ExecucaoService.Domain.csproj", "src/ExecucaoService.Domain/"]
COPY ["src/ExecucaoService.Infrastructure/ExecucaoService.Infrastructure.csproj", "src/ExecucaoService.Infrastructure/"]
RUN dotnet restore "src/ExecucaoService.API/ExecucaoService.API.csproj"

COPY src/ src/
RUN dotnet publish "src/ExecucaoService.API/ExecucaoService.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
# Repetido de propósito: scanners (ex.: Sonar S6471) conferem que a imagem final não roda como root.
USER $APP_UID
ENTRYPOINT ["dotnet", "ExecucaoService.API.dll"]
