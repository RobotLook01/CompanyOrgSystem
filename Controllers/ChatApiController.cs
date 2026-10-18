using ChatLogic;
using ChatLogic.ChatLogic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CompanyOrgSystem.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatApiController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        // コンストラクタ（secrets.json や appsettings.json の情報を安全に読み込みます）
        public ChatApiController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 1. 取得窓口（GET）
        [HttpGet("messages")]
        public IActionResult GetMessages()
        {
            string connStr = GetConnectionString();
            ChatService service = new ChatService(connStr);
            var treeData = service.GetMessageTree();
            return Ok(treeData);
        }

        // 2. 新規・コメント送信窓口（POST）
        [HttpPost("sendWithFile")]
        public IActionResult SendMessage([FromBody] NewChatRequestModel request)
        {
            if (!CheckAdminPassword(request.AdminPassword)) return Forbid("合言葉が違います。");

            string connStr = GetConnectionString();
            using (var conn = new Npgsql.NpgsqlConnection(connStr))
            {
                string sql = "INSERT INTO chat_messages (parent_id, sender_name, content_markdown) VALUES (@ParentId, @SenderName, @ContentMarkdown);";
                Dapper.SqlMapper.Execute(conn, sql, new { ParentId = request.ParentId, SenderName = request.SenderName, ContentMarkdown = request.ContentMarkdown });
            }
            return Ok(new { success = true });
        }

        // 3. 変更（修正）用の窓口（POST）
        [HttpPost("updateMessage")]
        public IActionResult UpdateMessage([FromBody] NewChatRequestModel request)
        {
            if (!CheckAdminPassword(request.AdminPassword) || request.MessageId == null) return Forbid("エラーが発生しました。");

            string connStr = GetConnectionString(); // ★安全に初期化された接続文字列を取得

            // VB.NETのDLLを呼び出します！
            ChatService service = new ChatService(connStr);
            service.UpdateMessage(request.MessageId.Value, request.ContentMarkdown);

            return Ok(new { success = true });
        }

        // 4. 削除用の窓口（POST）
        [HttpPost("deleteMessage")]
        public IActionResult DeleteMessage([FromBody] NewChatRequestModel request)
        {
            if (!CheckAdminPassword(request.AdminPassword) || request.MessageId == null) return Forbid("エラーが発生しました。");

            string connStr = GetConnectionString(); // ★安全に初期化された接続文字列を取得

            // VB.NETのDLLを呼び出します！
            ChatService service = new ChatService(connStr);
            service.DeleteMessage(request.MessageId.Value);

            return Ok(new { success = true });
        }

        /// <summary>
        /// 🔒 【新設】接続文字列をどこからでも安全・確実に1箇所で初期化して返す共通関数
        /// </summary>
        private string GetConnectionString()
        {
            string connStr = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrEmpty(connStr))
            {
                // ユーザシークレット管理「ChatConnection」から安全にパスワードを取得
                //connStr = _configuration.GetConnectionString("ChatConnection");
                connStr = _configuration.GetConnectionString("DefaultConnection");
            }
            return connStr;
        }

        // 🔒 合言葉の共通安全チェックバリデーション
        private bool CheckAdminPassword(string inputPass)
        {
            // ユーザシークレット管理「ChatConnection」から安全にパスワードを取得
            string AdminPassword = _configuration.GetConnectionString("ChatConnection");
            string correctPassword = Environment.GetEnvironmentVariable("ADMIN_PASS") ?? AdminPassword;
            return inputPass == correctPassword;
        }
    }

    public class NewChatRequestModel
    {
        public string AdminPassword { get; set; }
        public int? MessageId { get; set; }
        public int? ParentId { get; set; }
        public string SenderName { get; set; }
        public string ContentMarkdown { get; set; }
    }
}
