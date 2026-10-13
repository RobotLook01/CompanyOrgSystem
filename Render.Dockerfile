# 1. ビルド用の環境（自動置換を回避し、DockerHub正規ミラーから最新の.NET 10.0 SDKを取得）
FROM docker.io/mcr/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# プロジェクトファイルをコピーして復元
COPY *.csproj ./
RUN dotnet restore

# すべてのソースコードをコピーしてビルド
COPY . ./
RUN dotnet publish -c Release -o out

# 2. 実行用の環境（自動置換を回避し、DockerHub正規ミラーから最新の.NET 10.0 ASP.NET Runtimeを取得）
FROM docker.io/mcr/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/out .

# Renderのポート10000番にバインドする設定
ENV ASPNETCORE_URLS=http://+:10000

# 起動するDLLを指定
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
