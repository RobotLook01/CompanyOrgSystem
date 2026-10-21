# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /app

# ソリューションファイルと、ルート直下にあるC#プロジェクトファイルをコピー
COPY *.sln ./
COPY *.slnx ./
COPY *.csproj ./

# 独立して配置されているVB.NET（ChatLogic）の構造を正確にコピー
#COPY ChatLogic/*.vbproj ./ChatLogic/
# フォルダ名の大文字小文字に依存せず、すべての .vbproj を一括でコピー環境へ配置します
COPY */*.vbproj ./

# ソリューション全体（C#とVB.NETの両方）の依存関係を復元
RUN dotnet restore

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
