# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env

# 💡 作業ディレクトリを /src に設定
WORKDIR /src

# リポジトリのすべてのファイルを /src 直下に一括コピー
COPY . .

# 💡【重要】1つ深い階層にダミーの作業ディレクトリを作り、そこからビルドを実行します
# これにより、.csproj から見て「1つ上の階層（/src）」に ChatLogic や ProductService が綺麗に並びます
WORKDIR /src/app

# 1つ上の階層にあるプロジェクトファイルを明示的に指定して実行
RUN dotnet restore "../CompanyOrgSystem.csproj"
RUN dotnet publish "../CompanyOrgSystem.csproj" -c Release -o /app -p:ErrorOnDuplicatePublishOutputFiles=false

# 2. 実行用の環境
FROM public.ecr.aws/sam/build-dotnet8:latest
WORKDIR /app
COPY --from=build-env /app .

ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
