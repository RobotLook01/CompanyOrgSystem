# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env

# メインプロジェクトは /app/CompanyOrgSystem に配置して、ローカルと同じ位置関係にする
WORKDIR /app/CompanyOrgSystem

# プロジェクトの構成ファイルをそれぞれの正しいフォルダにコピー
COPY CompanyOrgSystem.csproj ./
COPY ../ChatLogic/ChatLogic.vbproj /app/ChatLogic/
COPY ../ProductService/ProductService.vbproj /app/ProductService/

# C#のプロジェクトファイルを直接指定して依存関係を復元
RUN dotnet restore CompanyOrgSystem.csproj

# すべてのソースコードを、ローカルと同じ階層構造（並び順）になるようにコピー
COPY . ./
COPY ../ChatLogic/ /app/ChatLogic/
COPY ../ProductService/ /app/ProductService/

# 【超重要】重複ファイルによる衝突エラーを完全に無視する魔法のオプション（-p:ErrorOnDuplicatePublishOutputFiles=false）を追記
RUN dotnet publish CompanyOrgSystem.csproj -c Release -o out -p:ErrorOnDuplicatePublishOutputFiles=false

# 2. 実行用の環境
FROM public.ecr.aws/sam/build-dotnet8:latest
WORKDIR /app
COPY --from=build-env /app/CompanyOrgSystem/out .

ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
