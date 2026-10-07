using Microsoft.AspNetCore.Mvc;
using CompanyOrgSystem.Models;
using Npgsql;
using System.Data;

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
        public IActionResult Index()
        {
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
            return View("~/Views/Home/SearchScreen.cshtml", results);
        }

        // 🌳 ⑤ 管理者専用：組織図を再帰クエリでツリー表示する処理
        public IActionResult OrgTree()
        {
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

    }
}
