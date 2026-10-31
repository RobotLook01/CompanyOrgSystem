FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /src

COPY . .

RUN mkdir -p CompanyOrgSystem && \
    mv Controllers CompanyOrgSystem/ 2>/dev/null || true && \
    mv Views CompanyOrgSystem/ 2>/dev/null || true && \
    mv Models CompanyOrgSystem/ 2>/dev/null || true && \
    mv wwwroot CompanyOrgSystem/ 2>/dev/null || true && \
    mv Properties CompanyOrgSystem/ 2>/dev/null || true && \
    mv Program.cs CompanyOrgSystem/ 2>/dev/null || true && \
    mv appsettings.json CompanyOrgSystem/ 2>/dev/null || true && \
    mv libman.json CompanyOrgSystem/ 2>/dev/null || true && \
    mv CompanyOrgSystem.csproj CompanyOrgSystem/ 2>/dev/null || true

WORKDIR /src/CompanyOrgSystem

RUN dotnet restore "CompanyOrgSystem.csproj"
RUN dotnet publish "CompanyOrgSystem.csproj" -c Release -o /app -p:ErrorOnDuplicatePublishOutputFiles=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends libgssapi-krb5-2 && \
    rm -rf /var/lib/apt/lists/*

# 1. 最初に publish された資材一式をコピー
COPY --from=build-env /app .

# 🌟【最重要・追記】置き去りにされていた wwwroot フォルダを、実行環境の /app/wwwroot へ物理的にコピーします！
COPY --from=build-env /src/CompanyOrgSystem/wwwroot ./wwwroot

ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
