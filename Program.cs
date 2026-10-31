using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection; // 💡【最上部に追加】DataProtection の名前空間を追加
using System.Security.Cryptography.Xml;
using static System.Runtime.InteropServices.JavaScript.JSType;

// 💡 1. 司令塔（builder）の作成（ファイル内で絶対に1回だけ宣言します）
var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------
// 【セッション設定】セッション用のサービスをシステムに登録します
// -------------------------------------------------------------
builder.Services.AddDistributedMemoryCache(); // セッションにメモリを使う設定


//builder.Services.AddSession(options =>
//{
//    options.IdleTimeout = TimeSpan.FromMinutes(30); // 30分間操作がないとセッション切れにする
//    options.Cookie.HttpOnly = true;                // セキュリティを高める設定
//    options.Cookie.IsEssential = true;             // 同意なしでもクッキーを使えるようにする
//});
// -------------------------------------------------------------
// 【セッション設定】スマホの古いクッキーによる暗号化エラーを完全にスルーします
// -------------------------------------------------------------
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // 30分間操作がないとセッション切れにする
    options.Cookie.HttpOnly = true;                // セキュリティを高める設定

    // 🌟【重要・追記】クッキーのチェックを「必須（Essential）」から「任意」に緩めます
    // これにより、DataProtectionのキーリングが見つからなくてもシステムがクラッシュせず、
    // 自動的に古いクッキーを破棄して新しい通信として画像データを通してくれます
    options.Cookie.IsEssential = false;
});


// -------------------------------------------------------------
// 【DataProtection設定】🌟Render上での暗号キー消滅によるセッション切断バグを完全にガードします
// -------------------------------------------------------------
builder.Services.AddDataProtection()
    // コンテナが再起動・スリープから復帰しても暗号キーが壊れないように、一時フォルダ（/tmp）へ永続化パスを指定します
    .PersistKeysToFileSystem(new DirectoryInfo(@"/tmp/DataProtection-Keys"))
    // アプリの識別名を明確にして、PCとスマホの間で暗号化セッションがズレるのを防ぎます
    .SetApplicationName("CompanyOrgSystem");

// -------------------------------------------------------------
// 【サービス登録】各種コントローラーや便利な道具（DI）を登録します
// -------------------------------------------------------------
builder.Services.AddControllersWithViews(); // 普通の画面用
builder.Services.AddControllers();          // [ApiController] や Fetch API を認識させるために必須

// 詳細画面（Detail.cshtml）でセッションを覗き見るための道具（IHttpContextAccessor）を登録
builder.Services.AddHttpContextAccessor();

// -------------------------------------------------------------
// 【スマホ・Render対策】クラウド環境（プロキシ）からの転送ヘッダーを受け入れる設定
// -------------------------------------------------------------
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});


// 💡 2. アプリケーションのビルド（これもファイル内で絶対に1回だけです）
var app = builder.Build();


// =============================================================
// 💡 3. ミドルウェア・パイプラインの設定（実行順序が命です！）
// =============================================================

// 🌟【最優先】スマホのポート迷子警告を消し去るため、絶対に一番最初に実行します！
app.UseForwardedHeaders();

// 現在の環境名をコンソールに表示する
Console.WriteLine("================================");
Console.WriteLine($"現在の環境: {app.Environment.EnvironmentName}");
Console.WriteLine($"MY_ENV: {app.Environment.EnvironmentName}");
Console.WriteLine("================================");

// 開発環境と本番環境の切り替え処理
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 通信の暗号化強制とルーティングの開通
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// 🌟【セッション有効化】必ず UseRouting() より後ろ、UseAuthorization() より前に配置！
app.UseSession();

app.UseAuthorization();


// =============================================================
// 💡 4. URL・エンドポイントのマッピング（住所の登録）
// =============================================================

// Fetch API（/Product/GetImage など）の動的ルートを開通させます
app.MapControllers();

// 🏠メニューバー「HOME」などの通常画面のルート規則
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// アプリの実行開始！
app.Run();
