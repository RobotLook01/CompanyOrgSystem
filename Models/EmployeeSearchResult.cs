namespace CompanyOrgSystem.Models
{
    // 画面のグリッド（表）に表示する1行分のデータを定義するクラスです
    public class EmployeeSearchResult
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string DivisionType { get; set; } = string.Empty; // 「主務」か「兼務」か
        public string Email { get; set; } = string.Empty;
    }
}
