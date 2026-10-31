# --------------------------------------------------------------------------------
# 1. ビルド用の環境（ここは現在の設定のままで完璧です！）
# --------------------------------------------------------------------------------
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /src
COPY . .

RUN mkdir -p CompanyOrgSystem && \
    mv Controllers CompanyOrgSystem/ 2>/dev/null || true && \
    mv Views CompanyOrgSystem/ 2>/dev/null || true && \
    mv Models CompanyOrgSystem/ 2>/dev/null || true && \
    mv wwwroot CompanyOrgSystem/ 2>/dev/null || true && \
    mv Properties CompanyOrgSystem/ 2>/dev/null || true && \
    mv Program.cs CompanyOrgSystem/ 2>/dev/null || true && \
    mv appsettings.json CompanyOrgSystem/ 2>/dev/null || true && \
    mv libman.json CompanyOrgSystem/ 2>/dev/null || true && \
    mv CompanyOrgSystem.csproj CompanyOrgSystem/ 2>/dev/null || true

WORKDIR /src/CompanyOrgSystem
RUN dotnet restore "CompanyOrgSystem.csproj"
RUN dotnet publish "CompanyOrgSystem.csproj" -c Release -o /app -p:ErrorOnDuplicatePublishOutputFiles=false

# --------------------------------------------------------------------------------
# 2. 実行用の環境 🌟【ここを修正します！】
# --------------------------------------------------------------------------------
# ❌ 旧：FROM public.ecr.aws/sam/build-dotnet8:latest
# ⭕ 新：Microsoft公式の、Web配信・実行専用の軽量ASP.NETコンテナに切り替えます！
FROM ://microsoft.com AS runtime
WORKDIR /app

# ビルド環境から成果物をコピー
COPY --from=build-env /app .

# Renderが標準で要求する10000番ポートでWebサーバーを完全に開通させます
ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
