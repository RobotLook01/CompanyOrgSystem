using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Cryptography.Xml;
using static System.Runtime.InteropServices.JavaScript.JSType;

var builder = WebApplication.CreateBuilder(args);
// -------------------------------------------------------------
// 【追加その1】セッション用のサービスをシステムに登録します
// -------------------------------------------------------------
builder.Services.AddDistributedMemoryCache(); // セッションにメモリを使う設定
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // 30分間操作がないとセッション切れにする
    options.Cookie.HttpOnly = true;                // セキュリティを高める設定
    options.Cookie.IsEssential = true;             // 同意なしでもクッキーを使えるようにする
});
// -------------------------------------------------------------


// Add services to the container.
builder.Services.AddControllersWithViews(); // 普通の画面用
// ★【追記】もし無ければ、すぐ下にこの1行を追加して丸写ししてください！
builder.Services.AddControllers(); // ← これが [ApiController] を認識させるために必須です

// ★★★【追記】この1行を丸写しで追加してください！ ★★★
//------------------------------------------------------------------------------------------------------------------------//
//詳細画面（Detail.cshtml）の最上部に書かれている @inject IHttpContextAccessor HttpContextAccessor という記述は、
//「ASP.NET Coreのシステムよ、セッションを覗き見るための道具（IHttpContextAccessor）をここに注入してくれ！」という
//DI（依存関係の注入）の命令です。
//Program.cs 側に builder.Services.AddHttpContextAccessor(); を書き足してあげることで、
//システムが「了解！いつでもその道具を貸し出せるように準備しておくね！」と登録を完了するため、
//画面を開いた瞬間にエラーにならずバチッと連動するようになります。
//------------------------------------------------------------------------------------------------------------------------//
builder.Services.AddHttpContextAccessor();


//------------------------------------------------------------------------------------------------------------------------//
//【解説】builder.Services.AddControllersWithViews();とは？
//
//　ASP.NET Core のプログラム（Program.cs）において、「MVC（Model-View-Controller）という仕組みを使って
//　画面（Webページ）を作るために必要な部品を、まとめてアプリに登録する」という超重要な役割をしています。
//　この1行を書くことで、アプリの中で以下の3つの機能（部品）が使えるようになります。
//
//　📦 登録される3つの主要な機能
//　1. Controller（コントローラー）の機能
//	　• URL（例: / Home / Privacy）を受け取って、どのC#プログラムを動かすかを割り振る「司令塔」の機能です。
//　2. View（ビュー / Razor）の機能
//	　• HTMLの中にC#のコードを埋め込める仕組み（.cshtml ファイル）を読み込み、ブラウザが読める普通のHTMLデータに変換して
//	 　 出力する機能です。
//　3. データの検証（Validation）やモデルバインディングの機能
//	　• 画面のフォームから送信されてきたデータを、C#のクラス（Model）に自動で変換したり、入力チェック（必須チェックなど）を
//	  　行ったりする裏方の機能です。
//------------------------------------------------------------------------------------------------------------------------//

using Microsoft.AspNetCore.HttpOverrides; // 💡 先頭にこのusingを追加

var builder = WebApplication.CreateBuilder(args);

// --- 💡 builder.Build() の手前に以下の設定を追加 ---
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // クラウド環境（Render等）ではプロキシのIPが動的に変わるため、安全のために制限をクリアします
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

//----------------------------------------------------------------------------------------------------------------//
//【解説】開発環境と本番環境の切り替えについて
//
//プログラム（Program.cs）の中では、以下のように判断しています。
//1.アプリ起動時に、OSの環境変数 ASPNETCORE_ENVIRONMENT の中身を見に行く。
//2. 中身が "Development" だったら、app.Environment.IsDevelopment() が True になる。
//3. 中身が "Production"（または文字が何も入っていない）だったら、False になる。
//このように、「プログラムを書き換えなくても、動かす場所（環境変数）を変えるだけで挙動が自動で切り替わる」
//という親切な設計になっています。
//
//----------------------------------------------------------------------------------------------------------------//
// アプリ起動時に、現在の環境名をコンソール（出力ウインドウ）に表示する
Console.WriteLine($"現在の環境: {app.Environment.EnvironmentName}");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();

    //----------------------------------------------------------------------------------------------------------------------------------------------//
    //【解説】  
    //
    //開発中のローカル環境ではエラーの詳細（ソースコードのどこで落ちたかなど）が画面に出たほうが便利ですが、
    //本番環境でそれが見えてしまうとセキュリティ上非常に危険です。
    //そのため、このコードを使って本番環境だけの安全対策を一括で設定しています。
    //具体的には、中に書かれている2つの命令が以下の役割を果たしています。
    //  1.app.UseExceptionHandler("/Home/Error");
    //    • 役割： 本番環境でシステムエラー（500 Internal Server Error）が発生した際、
    //             ユーザーをエラー専用ページ（/ Home / Error）へ自動的に案内（リダイレクト）します。
    //    • メリット： ユーザーに英語の不親切なエラー画面を見せる代わりに、「申し訳ありません、エラーが発生しました」といった
    //                 丁寧なオリジナルのエラー画面を表示させることができます。
    //  2.app.UseHsts();（HTTP Strict Transport Security）
    //   • 役割： ブラウザに対して、「このサイトと通信するときは、今後絶対に HTTP ではなく、暗号化された HTTPS だけを使いなさい」と
    //            強制する命令（セキュリティヘッダー）を送ります。
    //   • メリット： ユーザーが間違って http://... でアクセスしようとしても、ブラウザ側が自動で https://... に書き換えて接続してくれるようになり、
    //                通信の盗聴や偽装（中間者攻撃）を強力に防ぎます。
    //   • 注意点： コメントに「デフォルトは30日（The default HSTS value is 30 days.）」とある通り、一度アクセスしたブラウザはその期間中ずっと
    //              HTTPSでの接続を強制するようになります。
    //              開発環境でこれが有効になるとローカルでのテストが不便になるため、この if 文を使って「本番環境のみ」に制限しています。
    //
    //💡 ざっくりまとめると
    //この処理があるおかげで、私たちが自分のパソコンで動かすときは 「エラーの詳細が見えて開発しやすい状態」 になり、
    //いざ本番サーバーに公開したときは自動的に 「エラーの裏側を隠し、通信を暗号化する安全な状態」 に切り替わってくれます.
    //----------------------------------------------------------------------------------------------------------------------------------------------//

}


app.UseHttpsRedirection();
app.UseRouting();

// -------------------------------------------------------------
// 【追加その2】セッションのミドルウェアを有効にします
// ※ ⚠️ 必ず app.UseRouting() より後ろ、app.UseAuthorization() より前に書いてください！
// -------------------------------------------------------------
app.UseSession();
// -------------------------------------------------------------


app.UseAuthorization();

app.UseStaticFiles();    //app.MapStaticAssets();


// ★【追記】app.MapControllerRoute(...) の「すぐ上」あたりにこの1行を追記して丸写ししてください！
app.MapControllers(); // ← これが [Route("api/product")] の住所を開通させます


// 🏠メニューバー「HOME」の処理
app.MapControllerRoute(
    name: "default",
    //------------------------------------------------------------------------------------------------//
    //【解説】
    //
    //この設定（特に {controller=Home}）があるため、URLに Home が書かれていなくても
    //自動的に Home コントローラーとして処理され、エラーになりません。
    //------------------------------------------------------------------------------------------------//
    pattern: "{controller=Home}/{action=Index}/{id?}");
//pattern: "{controller=Home}/{action=Index}/{id?}")
//.WithStaticAssets();


// アプリ起動時に、現在の環境名をコンソール（出力ウインドウ）に表示する
Console.WriteLine("================================");
Console.WriteLine($"MY_ENV: {app.Environment.EnvironmentName}");
Console.WriteLine("================================");

app.Run();
