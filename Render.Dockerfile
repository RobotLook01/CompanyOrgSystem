# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env

# 💡【超重要】メインプロジェクトを1つ下げた /src/CompanyOrgSystem 階層に配置します
WORKDIR /src/CompanyOrgSystem

# リポジトリのすべてのファイルを、1つ上の階層（/src）に一括コピーします
COPY . /src/

# 依存関係の復元とパブリッシュを実行
# これにより、.csproj から見た「../ChatLogic」や「../ProductService」のパスが完璧に成立します
RUN dotnet restore "CompanyOrgSystem.csproj"
RUN dotnet publish "CompanyOrgSystem.csproj" -c Release -o /app -p:ErrorOnDuplicatePublishOutputFiles=false

# 2. 実行用の環境
FROM public.ecr.aws/sam/build-dotnet8:latest
WORKDIR /app
COPY --from=build-env /app .

ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
