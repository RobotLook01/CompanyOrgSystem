# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /app

# 【重要】C#側だけでなく、お隣にある「ChatLogic」のフォルダ構造もまとめてコンパイル環境に再現します
COPY *.sln ./
COPY *.slnx ./
COPY *.csproj ./
COPY ../ChatLogic/ChatLogic.vbproj /ChatLogic/

# ソリューションを指定して依存関係を復元
RUN dotnet restore CompanyOrgSystem.slnx

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
