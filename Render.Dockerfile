# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /app

# プロジェクトの構成ファイルをコピー
COPY *.sln ./
COPY *.slnx ./
COPY *.csproj ./
# ★修正：Dockerfileと同じ階層（または配下）にあるため、../ は不要です
COPY ChatLogic/ChatLogic.vbproj ./ChatLogic/
COPY ProductService/ProductService.vbproj ./ProductService/

# C#のプロジェクトファイルを直接指定して依存関係を復元
RUN dotnet restore CompanyOrgSystem.csproj

# すべてのソースコード（C#）とお隣のソースコード（VB.NET）をまとめてコピー
COPY . ./
# ★修正：こちらも同様に ../ を削除し、Docker内の正しいパスへコピーします
COPY ChatLogic/ ./ChatLogic/
COPY ProductService/ ./ProductService/

# 【超重要】重複ファイルによる衝突エラーを完全に無視する魔法のオプション（-p:ErrorOnDuplicatePublishOutputFiles=false）を追記
RUN dotnet publish CompanyOrgSystem.csproj -c Release -o out -p:ErrorOnDuplicatePublishOutputFiles=false

# 2. 実行用の環境
FROM public.ecr.aws/sam/build-dotnet8:latest
WORKDIR /app
COPY --from=build-env /app/out .

ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
