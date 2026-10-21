# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /app

# ソリューションファイルと、ルート直下にあるC#プロジェクトファイルをコピー
COPY *.sln ./
COPY *.slnx ./
COPY *.csproj ./

# 完璧にアップロードされたChatLogic（VB.NET）の構造を正確にコピー
COPY ChatLogic/*.vbproj ./ChatLogic/

# 【修正箇所】迷わないように明確にソリューションファイルを指定して依存関係を復元
RUN dotnet restore CompanyOrgSystem.slnx

# すべてのソースコードをコピーしてビルド・パブリッシュ
COPY . ./
RUN dotnet publish CompanyOrgSystem.csproj -c Release -o out

# 2. 実行用の環境（AWS公式パブリックレジストリから.NET 8.0 ランタイム環境を取得）
FROM public.ecr.aws/sam/build-dotnet8:latest
WORKDIR /app
COPY --from=build-env /app/out .

# Renderのポート10000番にバインドする設定
ENV ASPNETCORE_URLS=http://+:10000

# 起動するDLLを指定
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
