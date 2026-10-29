# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /src

# リポジトリのすべてのファイルを一括でDocker内にコピー
COPY . .

# 💡 修正：直下にあるプロジェクトファイルを指定して依存関係を復元
RUN dotnet restore "CompanyOrgSystem.csproj"

# 💡 修正：現在のフォルダ（直下）のまま、ビルド＆発行処理を実行
RUN dotnet publish "CompanyOrgSystem.csproj" -c Release -o /app -p:ErrorOnDuplicatePublishOutputFiles=false

# 2. 実行用の環境
FROM public.ecr.aws/sam/build-dotnet8:latest
WORKDIR /app
COPY --from=build-env /app .

ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
