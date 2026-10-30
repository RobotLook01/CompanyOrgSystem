# 1. ビルド用の環境（AWS公式パブリックレジストリから.NET 8.0 SDKを取得）
FROM public.ecr.aws/sam/build-dotnet8:latest AS build-env
WORKDIR /src

# ① まずすべてのファイルを一時的に /src にコピー
COPY . .

# ② 【超重要・完全版】wwwrootやPropertiesも含め、メインプロジェクトの資材をすべてフォルダ内に移動する
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

# ③ メインプロジェクトのフォルダに移動してビルドを実行
WORKDIR /src/CompanyOrgSystem

# これにより、.csproj から見た「../ChatLogic」が物理的に100%成立します
RUN dotnet restore "CompanyOrgSystem.csproj"
RUN dotnet publish "CompanyOrgSystem.csproj" -c Release -o /app -p:ErrorOnDuplicatePublishOutputFiles=false

# 2. 実行用の環境
FROM public.ecr.aws/sam/build-dotnet8:latest
WORKDIR /app
COPY --from=build-env /app .

ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "CompanyOrgSystem.dll"]
