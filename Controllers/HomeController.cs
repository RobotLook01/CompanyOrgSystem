using Microsoft.AspNetCore.Mvc;
using CompanyOrgSystem.Models;
using Npgsql;
using System.Data;
using Microsoft.AspNetCore.Http; // セッションを使うために必要

namespace CompanyOrgSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;

        // 🏠メニューバー「Privacy」の処理
        public IActionResult Privacy()
        {
            return View();
        }
        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 🏠 ① 最初に開く「メニュー画面」の処理
        //public IActionResult Index()
        //{
        //    return View();
        //}
        public IActionResult Index(bool isAdmin = false)
        {
            // 💡 最初の画面表示の時点でも、裏でセッションに状態を保存しておく
            HttpContext.Session.SetString("IsAdmin", isAdmin ? "true" : "false");

            ViewBag.IsAdmin = isAdmin;
            return View();
        }

        // 👥 ② 一般ユーザー向けの検索画面（初期表示）
        public IActionResult GeneralSearch()
        {
            var emptyList = new List<EmployeeSearchResult>();
            return View("~/Views/Home/SearchScreen.cshtml", emptyList);
        }

        // 🔒 ③ 管理者エリアに入るためのパスワードチェック処理
        [HttpPost]
        public IActionResult AdminLogin(string password)
        {
            if (password == "admin123")
            {
                // 📌 【最重要の修正】ここでセッションに管理者であることを安全に保存します！
                HttpContext.Session.SetString("IsAdmin", "true");


                // 💡 ログイン直後は、過去の古い検索履歴（キーワード）を一度きれいに消去する
                HttpContext.Session.Remove("LastKeyword");


                ViewBag.IsAdmin = true;
                var emptyList = new List<EmployeeSearchResult>();
                return View("~/Views/Home/SearchScreen.cshtml", emptyList);
            }
            else
            {
                TempData["ErrorMessage"] = "パスワードが正しくありません。";
                return RedirectToAction("Index");
            }
        }

        // 🔎 ④ 検索実行処理（一般・管理者共通）
        [HttpPost]
        public IActionResult DoSearch(string keyword, bool isAdmin)
        {
            // 💡 ここで「"true"」という文字列が確実にセットされているか確認してください
            HttpContext.Session.SetString("IsAdmin", isAdmin ? "true" : "false");


            // 【重要】null対策をして、入力された文字を確実にセッションに保存
            string savedKeyword = keyword ?? "";
            HttpContext.Session.SetString("LastKeyword", savedKeyword);
            //HttpContext.Session.SetString("LastKeyword", keyword ?? ""); // ★追加

            var results = new List<EmployeeSearchResult>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

            // 【対策1】null、空文字、前後の不要なスペースをすべて綺麗にして、確実に未入力判定する
            string cleanKeyword = (keyword ?? "").Trim();
            bool hasKeyword = !string.IsNullOrEmpty(cleanKeyword);

            // SQLのベース部分
            string sql = @"
        SELECT 
            e.employee_id, 
            e.employee_name, 
            d.department_name,
            CASE WHEN b.is_main_job = TRUE THEN '主務' ELSE '兼務' END AS division_type,
            e.email
        FROM employees e
        INNER JOIN belongs b ON e.employee_id = b.employee_id
        INNER JOIN departments d ON b.department_id = d.department_id";

            if (hasKeyword)
            {
                // SQL文の中に直接 % を書かず、パラメータをそのまま結合する形にします
                sql += " WHERE e.employee_name LIKE @Keyword";
            }

            sql += " ORDER BY e.employee_id, b.is_main_job DESC";

            using (var conn = new NpgsqlConnection(connectionString))
            {
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    if (hasKeyword)
                    {
                        // 【対策2】ユーザーが入力した「%」や「_」を通常の文字として検索できるようエスケープします
                        // PostgreSQLのLIKE演算子で「%」自体を検索したい場合は「\%」にする必要があります
                        string escapedKeyword = cleanKeyword.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

                        // C#側で前後に % を付与してパラメータにセットします
                        cmd.Parameters.Add("@Keyword", NpgsqlTypes.NpgsqlDbType.Varchar, 100).Value = $"%{escapedKeyword}%";
                    }

                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new EmployeeSearchResult
                            {
                                EmployeeId = reader.GetInt32(0),
                                EmployeeName = reader.GetString(1),
                                DepartmentName = reader.GetString(2),
                                DivisionType = reader.GetString(3),
                                Email = reader.IsDBNull(4) ? "" : reader.GetString(4)
                            });
                        }
                    }
                }
            }

            ViewBag.IsAdmin = isAdmin;
            ViewBag.Keyword = savedKeyword; // 💡 入力欄に文字を残すためにViewBagに入れる

            //return View("Search", model); 
            return View("~/Views/Home/SearchScreen.cshtml", results);
        }

        // 🌳 ⑤ 管理者専用：組織図を再帰クエリでツリー表示する処理
        public IActionResult OrgTree()
        {
            // 💡 サーバーのセッションから安全に権限をチェックする
            string sessionAdmin = HttpContext.Session.GetString("IsAdmin") ?? "false";
            bool isAdmin = sessionAdmin == "true";

            // 🔴 一般ユーザーが不正にURLを直に叩いてアクセスしてきたら弾く（セキュリティ強化）
            if (!isAdmin)
            {
                return RedirectToAction("Index"); // メニューに戻す
            }

            ViewBag.IsAdmin = true;
            var treeData = new List<OrgTreeResult>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

            // 💡 ぽすぐれの強力な「再帰クエリ（WITH RECURSIVE）」のSQL文です！
            string sql = @"
                WITH RECURSIVE org_chart AS (
                    -- 1. 最上位の組織（アンカー部）
                    SELECT 
                        department_id, 
                        department_name, 
                        parent_id, 
                        1 AS level,
                        CAST(department_name AS text) AS path
                    FROM departments
                    WHERE parent_id IS NULL

                    UNION ALL

                    -- 2. 子組織を再帰的に結合（再帰部）
                    SELECT 
                        d.department_id, 
                        d.department_name, 
                        d.parent_id, 
                        oc.level + 1 AS level,
                        oc.path || ' > ' || d.department_name AS path
                    FROM departments d
                    INNER JOIN org_chart oc ON d.parent_id = oc.department_id
                )
                SELECT department_id, department_name, parent_id, level, path 
                FROM org_chart
                ORDER BY path;";

            using (var conn = new NpgsqlConnection(connectionString))
            {
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            treeData.Add(new OrgTreeResult
                            {
                                DepartmentId = reader.GetInt32(0),
                                DepartmentName = reader.GetString(1),
                                ParentId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                                Level = reader.GetInt32(3),
                                Path = reader.GetString(4)
                            });
                        }
                    }
                }
            }

            // 階層データを持って専用の画面を表示します
            return View("~/Views/Home/OrgTree.cshtml", treeData);
        }

        // 📌 【新設】Controllers/HomeController.cs の一番下などに以下を追加してください
        [HttpGet]
        public IActionResult BackToSearch()
        {
            // セッションから状態を安全に読み取る
            string sessionAdmin = HttpContext.Session.GetString("IsAdmin") ?? "false";
            bool isAdmin = sessionAdmin == "true";

            // 💡 セッションから「最後に検索したキーワード」を確実に取り出す
            string lastKeyword = HttpContext.Session.GetString("LastKeyword") ?? "";

            var results = new List<EmployeeSearchResult>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

            string cleanKeyword = lastKeyword.Trim();

            // 【重要】文字が入っているかどうかにかかわらず、前回の状態と同じSQLを組み立てる
            string sql = @"
            SELECT 
                e.employee_id, 
                e.employee_name, 
                d.department_name,
                CASE WHEN b.is_main_job = TRUE THEN '主務' ELSE '兼務' END AS division_type,
                e.email
            FROM employees e
            INNER JOIN belongs b ON e.employee_id = b.employee_id
            INNER JOIN departments d ON b.department_id = d.department_id";

            bool hasKeyword = !string.IsNullOrEmpty(cleanKeyword);
            if (hasKeyword)
            {
                sql += " WHERE e.employee_name LIKE @Keyword";
            }

            sql += " ORDER BY e.employee_id, b.is_main_job DESC";

            using (var conn = new NpgsqlConnection(connectionString))
            {
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    if (hasKeyword)
                    {
                        string escapedKeyword = cleanKeyword.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
                        cmd.Parameters.Add("@Keyword", NpgsqlTypes.NpgsqlDbType.Varchar, 100).Value = $"%{escapedKeyword}%";
                    }

                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new EmployeeSearchResult
                            {
                                EmployeeId = reader.GetInt32(0),
                                EmployeeName = reader.GetString(1),
                                DepartmentName = reader.GetString(2),
                                DivisionType = reader.GetString(3),
                                Email = reader.IsDBNull(4) ? "" : reader.GetString(4)
                            });
                        }
                    }
                }
            }

            ViewBag.IsAdmin = isAdmin;
            ViewBag.Keyword = lastKeyword; // 💡 入力欄に文字を復活させる
            return View("~/Views/Home/SearchScreen.cshtml", results);
        }

        //    // -------------------------------------------------------------
        //    // 🌳 ⑥-1：【新設】ビジュアル組織図エディタ画面の表示
        //    // -------------------------------------------------------------
        //    [HttpGet]
        //    public IActionResult OrgEditScreen()
        //    {
        //        // セッションから安全に権限をチェック
        //        string sessionAdmin = HttpContext.Session.GetString("IsAdmin") ?? "false";
        //        if (sessionAdmin != "true")
        //        {
        //            return RedirectToAction("Index"); // 一般ユーザーならメニューに弾く
        //        }

        //        string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
        //        string jsonResult = "[]"; // 初期値（空配列）

        //        // 💡 ぽすぐれ側で組織図ライブラリが直接読めるJSONを爆速生成するSQL
        //        string sql = @"
        //            WITH RECURSIVE org_chart AS (
        //                SELECT department_id, department_name, parent_id, 1 AS level, CAST(department_name AS text) AS path
        //                FROM departments WHERE parent_id IS NULL
        //                UNION ALL
        //                SELECT d.department_id, d.department_name, d.parent_id, oc.level + 1 AS level, oc.path || ' > ' || d.department_name AS path
        //                FROM departments d
        //                INNER JOIN org_chart oc ON d.parent_id = oc.department_id
        //            )
        //            SELECT COALESCE(json_strip_nulls(json_agg(json_build_object(
        //                'id',  department_id,
        //                'pid', parent_id,
        //                'name', department_name,
        //                'level', level,
        //                'path', path
        //            ))), '[]'::json)
        //            FROM org_chart;";

        //        using (var conn = new NpgsqlConnection(connectionString))
        //        {
        //            using (var cmd = new NpgsqlCommand(sql, conn))
        //            {
        //                conn.Open();
        //                var scalarResult = cmd.ExecuteScalar();
        //                if (scalarResult != DBNull.Value && scalarResult != null)
        //                {
        //                    jsonResult = scalarResult.ToString();
        //                }
        //            }
        //        }

        //        // 💡 JavaScriptにそのまま渡せるように生JSONをViewBagへ
        //        ViewBag.OrgJsonData = jsonResult;
        //        ViewBag.IsAdmin = true;

        //        return View("~/Views/Home/OrgEditScreen.cshtml");
        //    }

        //    // -------------------------------------------------------------
        //    // 🌳 ⑥-2：【新設】画面でドラッグ＆ドロップされた時にDBを更新するAPI
        //    // -------------------------------------------------------------
        //    [HttpPost]
        //    public IActionResult UpdateOrgParent([FromBody] OrgMoveRequest request)
        //    {
        //        // 安全のためセッション権限チェック
        //        string sessionAdmin = HttpContext.Session.GetString("IsAdmin") ?? "false";
        //        if (sessionAdmin != "true") { return Forbid(); }

        //        if (request.DepartmentId == request.NewParentId)
        //        {
        //            return BadRequest(new { success = false, message = "自分自身を親にすることはできません。" });
        //        }

        //        string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

        //        // 💡 親組織（parent_id）を上書き更新するSQL
        //        string sql = @"
        //            UPDATE departments 
        //            SET parent_id = @NewParentId 
        //            WHERE department_id = @DepartmentId;";

        //        using (var conn = new NpgsqlConnection(connectionString))
        //        {
        //            using (var cmd = new NpgsqlCommand(sql, conn))
        //            {
        //                cmd.Parameters.AddWithValue("@DepartmentId", request.DepartmentId);
        //                cmd.Parameters.AddWithValue("@NewParentId", request.NewParentId ?? (object)DBNull.Value);

        //                conn.Open();
        //                cmd.ExecuteNonQuery();
        //            }
        //        }

        //        return Ok(new { success = true, message = "組織構造をリアルタイム更新しました！" });
        //    }
        //}

        // 💡 画面から届くドラッグデータを型安全に受け取るためのクラス
        //public class OrgMoveRequest
        //{
        //    public int DepartmentId { get; set; }
        //    public int? NewParentId { get; set; }
        //}

    }
}