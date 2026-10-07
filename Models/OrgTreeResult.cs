namespace CompanyOrgSystem.Models
{
    // 組織図の階層データを受け取るための専用の器です
    public class OrgTreeResult
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public int Level { get; set; }          // 階層の深さ（1:本社、2:本部、3:課）
        public string Path { get; set; } = string.Empty; // 階層のルート文字列
    }
}
