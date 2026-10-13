# 1. ビルド用の環境（DockerHub正規ミラーから.NET 8.0 SDKを取得）
FROM docker.io/mcr/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# プロジェクトファイルをコピーして復元
COPY *.csproj ./
RUN dotnet restore

# すべてのソースコードをコピーしてビルド
COPY . ./
RUN dotnet publish -c Release -o out

# 2. 実行用の環境（DockerHub正規ミラーからASP.NET Core Runtimeを取得）
FROM docker.io/mcr/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Renderのポート10000番にバインドする設定
ENV ASPNETCORE_URLS=http://+:10000

# 起動するDLLを指定
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
