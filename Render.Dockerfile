# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /app

# ソリューションファイルと、各プロジェクトの構造を正確にコピーして復元
# ※VB.NETのフォルダ名は実際の名称（例: VbLibraryProject など）に書き換えてください。
COPY *.sln ./
COPY CompanyOrgSystem/*.csproj ./CompanyOrgSystem/
COPY ChatLogic/*.vbproj ./ChatLogic/

# ソリューション全体（C#とVB.NETの両方）の依存関係を復元
RUN dotnet restore

# すべてのソースコードをコピーしてビルド・パブリッシュ
COPY . ./
RUN dotnet publish CompanyOrgSystem/CompanyOrgSystem.csproj -c Release -o out

# 2. 実行用の環境（AWS公式パブリックレジストリから.NET 8.0 ランタイム環境を取得）
FROM public.ecr.aws/sam/build-dotnet8:latest
WORKDIR /app
COPY --from=build-env /app/out .

# Renderのポート10000番にバインドする設定
ENV ASPNETCORE_URLS=http://+:10000

# 起動するDLLを指定
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
