# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /app

# プロジェクトの構成ファイルをコピー
COPY *.sln ./
COPY *.slnx ./
COPY *.csproj ./
COPY ../ChatLogic/ChatLogic.vbproj /ChatLogic/

# C#のプロジェクトファイルを直接指定して依存関係を復元
RUN dotnet restore CompanyOrgSystem.csproj

# すべてのソースコード（C#）とお隣のソースコード（VB.NET）をまとめてコピー
COPY . ./
COPY ../ChatLogic/ /ChatLogic/

# 【超重要】重複ファイルによる衝突エラーを完全に無視する魔法のオプション（-p:ErrorOnDuplicatePublishOutputFiles=false）を追記
RUN dotnet publish CompanyOrgSystem.csproj -c Release -o out -p:ErrorOnDuplicatePublishOutputFiles=false

# 2. 実行用の環境
FROM public.ecr.aws/sam/build-dotnet8:latest
WORKDIR /app
COPY --from=build-env /app/out .

ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
