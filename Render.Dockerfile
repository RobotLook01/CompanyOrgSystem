# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /src

# ソリューション内のすべてのプロジェクトファイル（.csproj, .vbproj）を階層構造を維持して一括コピー
COPY CompanyOrgSystem.sln ./
COPY *.slnx ./
COPY CompanyOrgSystem/*.csproj ./CompanyOrgSystem/
COPY ChatLogic/*.vbproj ./ChatLogic/
COPY ProductService/*.vbproj ./ProductService/

# 依存関係の復元
RUN dotnet restore "CompanyOrgSystem/CompanyOrgSystem.csproj"

# すべてのソースコードを一括でコピー
COPY . .

# メインプロジェクトのビルドおよび発行（成果物を /app に出力）
WORKDIR /src/CompanyOrgSystem
RUN dotnet publish CompanyOrgSystem.csproj -c Release -o /app -p:ErrorOnDuplicatePublishOutputFiles=false

# 2. 実行用の環境
FROM public.ecr.aws/sam/build-dotnet8:latest
WORKDIR /app
COPY --from=build-env /app .

ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
