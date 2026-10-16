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

        /// <summary>
        /// 🌳 ⑤ 管理者専用：環境変数ガード版組織図ツリー表示（Git公開完全対応・一撃表示版）
        /// </summary>
        /// <param name="securet">URLの末尾の秘密のキー</param>
        public IActionResult OrgTree(string securet)
        {
            // 💡 URLのパラメータ名が「secret」で届く可能性も考慮して両方で受け取れるように二段構えにします
            if (string.IsNullOrEmpty(securet))
            {
                securet = HttpContext.Request.Query["secret"];
            }

            // 1. サーバーのセッションから安全に管理者権限をチェック
            string sessionAdmin = HttpContext.Session.GetString("IsAdmin") ?? "false";

            // 2. 💡【Git安全設計】Renderの環境変数から、秘密の鍵を読み込む
            string realSecret = _configuration["SECRET_KEY"] ?? "";

            // 🌟【最重要のセキュリティ修正：一撃アクセスの許可】
            // URLの末尾の合言葉がRenderの環境変数と完全に一致している場合は、
            // セッションログインしていなくても、その場で「本物の管理者だ！」とみなして中へ通します。
            bool isUrlUnlocked = !string.IsNullOrEmpty(securet) && !string.IsNullOrEmpty(realSecret) && (securet == realSecret);
            bool isAdmin = (sessionAdmin == "true" || isUrlUnlocked);

            // 🔴 合言葉も合っておらず、セッションログインもしていない本当の一般ユーザーだけを弾く
            if (!isAdmin)
            {
                return RedirectToAction("Index"); // メニューに戻す
            }

            // 💡 URLから一度正しい合言葉を受け取っていたら、次回の遷移のためにセッションにも管理者フラグをセットしておく
            if (isUrlUnlocked)
            {
                HttpContext.Session.SetString("IsAdmin", "true");
            }

            ViewBag.IsAdmin = true;
            var treeData = new System.Collections.Generic.List<OrgTreeResult>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

            // URLの末尾の合位置が一致している場合だけ【本番保存】を許可
            if (isUrlUnlocked == true && sessionAdmin == "true")
            {
                ViewBag.IsRealSave = true; // 本番保存モード（管理者専用）
                ViewData["ModeBadge"] = "🔒 本番データ更新モード（管理者専用）";
            }
            else
            {
                ViewBag.IsRealSave = false; // 体験デモモード（採用担当者用）
                ViewData["ModeBadge"] = "🎮 操作体験デモモード（採用担当者用）";
            }

            // 3. PostgreSQLからデータを取得する（安心の再帰クエリSQL文）
            string sql = @"
                WITH RECURSIVE org_chart AS (
                    SELECT 
                        department_id, 
                        department_name, 
                        parent_id, 
                        1 AS level,
                        CAST(department_name AS text) AS path
                    FROM departments
                    WHERE parent_id IS NULL AND is_deleted = FALSE

                    UNION ALL

                    SELECT 
                        d.department_id, 
                        d.department_name, 
                        d.parent_id, 
                        oc.level + 1 AS level,
                        oc.path || ' > ' || d.department_name AS path
                    FROM departments d
                    INNER JOIN org_chart oc ON d.parent_id = oc.department_id
                    WHERE d.is_deleted = FALSE
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
                                ParentId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                                Level = reader.GetInt32(3),
                                Path = reader.GetString(4)
                            });
                        }
                    }
                }
            }

            // 4. データをモデルに渡して、管理者用画面を表示！
            if (ViewBag.IsRealSave == true)
            {
                return View("~/Views/Home/OrgTree.cshtml", treeData);
            }
            else
            {
                return View("~/Views/Home/OrgTreeS.cshtml", treeData);
            }
        }

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

        /// <summary>
        /// 🔢 組織の上下の並び順（display_order）を入れ替える処理
        /// </summary>
        /// <param name="id">動かしたい組織のID</param>
        /// <param name="direction">"up" (上へ) または "down" (下へ)</param>
        public IActionResult MoveOrder(int id, string direction)
        {
            // appsettings.json から接続文字列を取得
            string connString = _configuration.GetConnectionString("DefaultConnection")
                             ?? _configuration["ConnectionStrings:DefaultConnection"];

            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();

                // 1. 動かしたい組織（自分）の現在の「親ID」と「表示順」を調べる
                int? parentId = null;
                int myOrder = 0;
                string selectMySql = "SELECT parent_id, display_order FROM departments WHERE department_id = @id";

                using (var cmd = new NpgsqlCommand(selectMySql, conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            parentId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                            myOrder = reader.GetInt32(1);
                        }
                        else
                        {
                            return RedirectToAction("OrgTree"); // 自分が存在しなければ戻る
                        }
                    }
                }

                // 2. 入れ替え対象となる「すぐ上」または「すぐ下」の相手を特定するSQLを組み立てる
                string targetSql = "";
                if (direction == "up")
                {
                    // 自分と同じ親で、自分より数字が小さくて、一番自分に近い（最大値の）相手
                    targetSql = parentId == null
                        ? "SELECT department_id, display_order FROM departments WHERE parent_id IS NULL AND display_order < @myOrder AND is_deleted = FALSE ORDER BY display_order DESC LIMIT 1"
                        : "SELECT department_id, display_order FROM departments WHERE parent_id = @parentId AND display_order < @myOrder AND is_deleted = FALSE ORDER BY display_order DESC LIMIT 1";
                }
                else
                {
                    // 自分と同じ親で、自分より数字が大きくて、一番自分に近い（最小値の）相手
                    targetSql = parentId == null
                        ? "SELECT department_id, display_order FROM departments WHERE parent_id IS NULL AND display_order > @myOrder AND is_deleted = FALSE ORDER BY display_order ASC LIMIT 1"
                        : "SELECT department_id, display_order FROM departments WHERE parent_id = @parentId AND display_order > @myOrder AND is_deleted = FALSE ORDER BY display_order ASC LIMIT 1";
                }

                int targetId = 0;
                int targetOrder = 0;
                bool hasTarget = false;

                using (var cmd = new NpgsqlCommand(targetSql, conn))
                {
                    if (parentId != null) cmd.Parameters.AddWithValue("parentId", parentId);
                    cmd.Parameters.AddWithValue("myOrder", myOrder);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            targetId = reader.GetInt32(0);
                            targetOrder = reader.GetInt32(1);
                            hasTarget = true; // 入れ替え相手が見つかった
                        }
                    }
                }

                // 3. 入れ替え相手がいない場合（すでに一番上、または一番下の場合）は何もしない
                if (!hasTarget)
                {
                    return RedirectToAction("OrgTree");
                }

                // 4. 【最重要】安全な一括トランザクションで、自分と相手の数値を「トレード」する
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string updateSql = "UPDATE departments SET display_order = @order WHERE department_id = @id";

                        // 自分の順序を相手の順序に上書き
                        using (var cmd = new NpgsqlCommand(updateSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("order", targetOrder);
                            cmd.Parameters.AddWithValue("id", id);
                            cmd.ExecuteNonQuery();
                        }

                        // 相手の順序を自分の元の順序に上書き
                        using (var cmd = new NpgsqlCommand(updateSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("order", myOrder);
                            cmd.Parameters.AddWithValue("id", targetId);
                            cmd.ExecuteNonQuery();
                        }

                        // 二つのUPDATEが両方成功したらコミットして確定！
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        // 万が一途中でエラーが起きたら巻き戻す
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            // 5. 処理が終わったら、最新の順序で組織図ツリー画面を再表示する
            return RedirectToAction("OrgTree");
        }

        /// <summary>
        /// ➕ 組織の階層を1段階深くする処理 (右へシフト)
        /// </summary>
        public IActionResult MakeDeeper(int id)
        {
            string connString = _configuration.GetConnectionString("DefaultConnection")
                             ?? _configuration["ConnectionStrings:DefaultConnection"];

            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();

                // 1. 自分の現在の「親ID」と「表示順」を調べる
                int? parentId = null;
                int myOrder = 0;
                string sql1 = "SELECT parent_id, display_order FROM departments WHERE department_id = @id";
                using (var cmd = new NpgsqlCommand(sql1, conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            parentId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                            myOrder = reader.GetInt32(1);
                        }
                    }
                }

                // 2. 同一階層内で、自分より1つ上にいる「新しい親候補」の部署IDを特定する
                string sql2 = parentId == null
                    ? "SELECT department_id FROM departments WHERE parent_id IS NULL AND display_order < @myOrder AND is_deleted = FALSE ORDER BY display_order DESC LIMIT 1"
                    : "SELECT department_id FROM departments WHERE parent_id = @parentId AND display_order < @myOrder AND is_deleted = FALSE ORDER BY display_order DESC LIMIT 1";

                int? newParentId = null;
                using (var cmd = new NpgsqlCommand(sql2, conn))
                {
                    if (parentId != null) cmd.Parameters.AddWithValue("parentId", parentId);
                    cmd.Parameters.AddWithValue("myOrder", myOrder);
                    var result = cmd.ExecuteScalar();
                    if (result != null) newParentId = Convert.ToInt32(result);
                }

                // 3. 上に部署がいれば、その部署を自分の「新しい親」に設定する
                if (newParentId != null)
                {
                    string updateSql = "UPDATE departments SET parent_id = @newParentId, display_order = 999 WHERE department_id = @id";
                    using (var cmd = new NpgsqlCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("newParentId", newParentId);
                        cmd.Parameters.AddWithValue("id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            return RedirectToAction("OrgTree");
        }

        /// <summary>
        /// ➖ 組織の階層を1段階浅くする処理 (左へシフト)
        /// </summary>
        public IActionResult MakeShallower(int id)
        {
            string connString = _configuration.GetConnectionString("DefaultConnection")
                             ?? _configuration["ConnectionStrings:DefaultConnection"];

            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();

                // 1. 現在の「親のID」を調べる
                int? currentParentId = null;
                string sql1 = "SELECT parent_id FROM departments WHERE department_id = @id";
                using (var cmd = new NpgsqlCommand(sql1, conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    var result = cmd.ExecuteScalar();
                    if (result != DBNull.Value && result != null) currentParentId = Convert.ToInt32(result);
                }

                // すでに最上位(親がNULL)なら、これ以上浅くできないので戻る
                if (currentParentId == null) return RedirectToAction("OrgTree");

                // 2. 「親の、そのまた親のID」を調べる
                int? grandParentId = null;
                string sql2 = "SELECT parent_id FROM departments WHERE department_id = @parent_id";
                using (var cmd = new NpgsqlCommand(sql2, conn))
                {
                    cmd.Parameters.AddWithValue("parent_id", currentParentId);
                    var result = cmd.ExecuteScalar();
                    if (result != DBNull.Value && result != null) grandParentId = Convert.ToInt32(result);
                }

                // 3. 親の親のID（ルートならNULL）を、自分の新しい親にセットして独立する
                string updateSql = "UPDATE departments SET parent_id = @grandParentId, display_order = 999 WHERE department_id = @id";
                using (var cmd = new NpgsqlCommand(updateSql, conn))
                {
                    cmd.Parameters.AddWithValue("grandParentId", (object)grandParentId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.ExecuteNonQuery();
                }
            }
            return RedirectToAction("OrgTree");
        }

        /// <summary>
        /// 📝 組織の名前をその場で変更する処理
        /// </summary>
        [HttpPost]
        public IActionResult RenameDepartment(int id, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return RedirectToAction("OrgTree");

            string connString = _configuration.GetConnectionString("DefaultConnection")
                             ?? _configuration["ConnectionStrings:DefaultConnection"];

            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                string sql = "UPDATE departments SET department_name = @name WHERE department_id = @id";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("name", newName.Trim());
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.ExecuteNonQuery();
                }
            }
            return RedirectToAction("OrgTree");
        }

        /// <summary>
        /// ➕ 新しい組織をデータベースに新規登録（インサート）する処理
        /// </summary>
        /// <param name="departmentName">画面から入力された新しい組織名</param>
        /// <param name="parentId">画面で選択された親組織のID（NULLを許容）</param>
        [HttpPost]
        public IActionResult AddDepartment(string departmentName, int? parentId)
        {
            if (string.IsNullOrWhiteSpace(departmentName))
            {
                return RedirectToAction("OrgTree");
            }

            string connString = _configuration.GetConnectionString("DefaultConnection")
                             ?? _configuration["ConnectionStrings:DefaultConnection"];

            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();

                // 1. 現在の「最大のdepartment_id」を調べて、新しいID（+1）を決める
                int newId = 1;
                string maxIdSql = "SELECT COALESCE(MAX(department_id), 0) FROM departments";
                using (var cmd = new NpgsqlCommand(maxIdSql, conn))
                {
                    newId = Convert.ToInt32(cmd.ExecuteScalar()) + 1;
                }

                // 2. 新しい組織を「最末尾」に並べるために、現在の同じ階層の最大display_orderを調べる
                int newOrder = 1;
                string maxOrderSql = parentId == null
                    ? "SELECT COALESCE(MAX(display_order), 0) FROM departments WHERE parent_id IS NULL"
                    : "SELECT COALESCE(MAX(display_order), 0) FROM departments WHERE parent_id = @parentId";

                using (var cmd = new NpgsqlCommand(maxOrderSql, conn))
                {
                    if (parentId != null) cmd.Parameters.AddWithValue("parentId", parentId);
                    newOrder = Convert.ToInt32(cmd.ExecuteScalar()) + 1;
                }

                // 3. PostgreSQL（ぽすぐれ）に新しい組織レコードを安全にインサートする
                string insertSql = @"
                    INSERT INTO departments (department_id, department_name, parent_id, display_order, is_deleted) 
                    VALUES (@id, @name, @parentId, @order, FALSE)";

                using (var cmd = new NpgsqlCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("id", newId);
                    cmd.Parameters.AddWithValue("name", departmentName.Trim());
                    cmd.Parameters.AddWithValue("parentId", (object)parentId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("order", newOrder);

                    cmd.ExecuteNonQuery(); // 実行して即時コミット！
                }
            }

            // 4. 登録が終わったら、追加された新しいツリー組織図をパッと再表示する
            return RedirectToAction("OrgTree");
        }

    }
}

