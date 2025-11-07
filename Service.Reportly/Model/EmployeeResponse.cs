namespace Service.Reportly.Model
{
    // Model phản ánh dữ liệu trả về từ API /api/employee/email/{email}
    public class EmployeeResponse
    {
        public int Id { get; set; }
        public string? Fullname { get; set; }
        public string? Email { get; set; }

        public int? Phone { get; set; }
        public string? Position { get; set; }
        public string? DepartmentName { get; set; }
        public string? JobPositionName { get; set; }
    }
}
