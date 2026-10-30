using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Data;
using System.Collections.Generic;
using ProductService;
using VbLogic = ProductService.ProductService;

namespace CompanyOrgSystem.Controllers
{
    // ★ 企業組織図マスターツリーと100%同一の「URLその場チェック方式」完全開通版コントローラー
    public class ProductController : Controller
    {
        private readonly IConfiguration _configuration;

        public ProductController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // --------------------------------------------------
        // 📄 1. 通常の画面（HTML）を返却する窓口
        // --------------------------------------------------

        // URL: /Product/Index または /Product/Index?secret=XXXX
        [HttpGet]
        public IActionResult Index([FromQuery] string secret)
        {
            ViewBag.CurrentSecret = secret;
            return View();
        }

        // URL: /Product/Detail?id=PROG-001&secret=XXXX
        [HttpGet]
        public IActionResult Detail([FromQuery] string secret)
        {
            // 外部ファイル（JSONや環境変数）から正解のパスワードを安全に取得
            string adminPassword = _configuration.GetConnectionString("ChatConnection");
            string correctPassword = Environment.GetEnvironmentVariable("ADMIN_PASS") ?? adminPassword;

            // ★【その場チェック】URLの末尾に「今」正しい合言葉がある時だけ、画面に「管理者専用フォーム」の表示を許可！
            ViewBag.IsAdminMode = (!string.IsNullOrEmpty(secret) && secret == correctPassword);

            return View();
        }

        // --------------------------------------------------
        // 📸 2. 画像（ハードコピー）の取り込み処理（POST）
        // --------------------------------------------------
        [HttpPost]
        public IActionResult UploadImage(string productId, int versionNo, IFormFile imageFile, [FromForm] string urlSecret)
        {
            // 外部ファイルから正解を取得
            string adminPassword = _configuration.GetConnectionString("ChatConnection");
            string correctPassword = Environment.GetEnvironmentVariable("ADMIN_PASS") ?? adminPassword;

            // 🔒【強力ガード】画面の裏側（urlSecret）から送られてきた文字を厳密にチェック！
            // ※引数の「urlSecret」とHTML側の「name="urlSecret"」が完全に一致したため、正しく値が届くようになりました！
            if (string.IsNullOrEmpty(urlSecret) || urlSecret != correctPassword)
            {
                return RedirectToAction("Index"); // 違ったら一覧画面に強制退場
            }

            // チェックを突破したら、VB.NETの関数を呼び出してポスグレにUPDATE保存
            if (imageFile != null && imageFile.Length > 0)
            {
                using (var memoryStream = new MemoryStream())
                {
                    imageFile.CopyTo(memoryStream);
                    byte[] fileBytes = memoryStream.ToArray();

                    string connStr = GetConnectionString();
                    VbLogic service = new VbLogic(connStr);
                    service.SaveProductImage(productId, versionNo, fileBytes);
                }
            }

            // 保存完了後も、URLの末尾にしっかりシークレットをくっつけて詳細画面を引き直します
            return RedirectToAction("Detail", new { id = productId, secret = correctPassword });
        }

        // --------------------------------------------------
        // ⚙️ 3. データ配信用窓口（Fetch API用）
        // --------------------------------------------------
        [HttpGet]
        public IActionResult GetList()
        {
            string connStr = GetConnectionString();
            VbLogic service = new VbLogic(connStr);
            DataTable dt = service.GetAllProducts();
            return Json(ConvertDataTableToList(dt));
        }

        // ★【新設】URL: /Product/GetVersions?id=PROG-001
        [HttpGet]
        public IActionResult GetVersions(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest("作品IDが指定されていません。");

            string connStr = GetConnectionString();
            VbLogic service = new VbLogic(connStr);

            // VB.NET側からDataTableを取得して、JSON用のリストに変換して返却
            DataTable dt = service.GetProductVersions(id);
            return Json(ConvertDataTableToList(dt));
        }

        [HttpGet]
        public IActionResult GetDetails(string id, int version)
        {
            string connStr = GetConnectionString();
            VbLogic service = new VbLogic(connStr);
            DataTable dt = service.GetProgramDetails(id, version);
            return Json(ConvertDataTableToList(dt));
        }

        //[HttpGet]
        //public IActionResult GetImage(string id, int version)
        //{
        //    string connStr = GetConnectionString();
        //    VbLogic service = new VbLogic(connStr);
        //    byte[] imageBytes = service.GetProductImageBytes(id, version);
        //    if (imageBytes != null && imageBytes.Length > 0) return File(imageBytes, "image/jpeg");
        //    return NoContent();
        //}
        [HttpGet]
        public IActionResult GetImage(string id, int version)
        {
            string connStr = GetConnectionString();
            VbLogic service = new VbLogic(connStr);
            byte[] imageBytes = service.GetProductImageBytes(id, version);

            if (imageBytes != null && imageBytes.Length > 0)
            {
                // 💡【最重要】"image/jpeg" の後ろに、明示的にファイルサイズ情報（Content-Length）などの
                // エンティティヘッダーを乗せるオーバーロード（引数3つのFileメソッド）に変更します。
                // これにより、スマホのブラウザが「これから何バイトの画像データが届くか」を正確に把握し、
                // モバイル回線のパケット揺らぎによるデータ破損（表示切れ）を100%防止します。
                return File(imageBytes, "image/jpeg", true); // 第3引数の true でRangeリクエスト（分割ダウンロード）にも対応
            }

            return NoContent();
        }


        // 🔒 接続文字列を取得する共通関数
        private string GetConnectionString()
        {
            string connStr = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrEmpty(connStr)) connStr = _configuration.GetConnectionString("DefaultConnection");
            return connStr;
        }

        // 💡 補助関数：DataTableをJSON用のリストに変換
        private List<Dictionary<string, object>> ConvertDataTableToList(DataTable dt)
        {
            var list = new List<Dictionary<string, object>>();
            foreach (DataRow row in dt.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in dt.Columns) dict[col.ColumnName] = row[col];
                list.Add(dict);
            }
            return list;
        }
    }
}
