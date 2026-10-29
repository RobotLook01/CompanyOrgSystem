# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /src

# リポジトリのすべてのファイルを一括でDocker内にコピー（階層構造を100%維持）
COPY . .

# 依存関係の復元（C#プロジェクトファイルを直接指定して復元します）
RUN dotnet restore "CompanyOrgSystem/CompanyOrgSystem.csproj"

# メインプロジェクトのフォルダに移動して、ビルド＆発行処理を実行
WORKDIR /src/CompanyOrgSystem
RUN dotnet publish CompanyOrgSystem.csproj -c Release -o /app -p:ErrorOnDuplicatePublishOutputFiles=false

# 2. 実行用の環境
FROM public.ecr.aws/sam/build-dotnet8:latest
WORKDIR /app
COPY --from=build-env /app .

ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
