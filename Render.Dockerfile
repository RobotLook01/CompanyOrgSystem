# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /app

# プロジェクトの構成ファイルをコピー
COPY *.sln ./
COPY *.slnx ./
COPY *.csproj ./
COPY ../ChatLogic/ChatLogic.vbproj /ChatLogic/

# 【修正箇所】.slnxではなく、C#のプロジェクトファイルを直接指定して依存関係を復元します
RUN dotnet restore CompanyOrgSystem.csproj

# すべてのソースコード（C#）とお隣のソースコード（VB.NET）をまとめてコピー
COPY . ./
COPY ../ChatLogic/ /ChatLogic/

# ビルド・パブリッシュ
RUN dotnet publish CompanyOrgSystem.csproj -c Release -o out

# 2. 実行用の環境
FROM public.ecr.aws/sam/build-dotnet8:latest
WORKDIR /app
COPY --from=build-env /app/out .

ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
