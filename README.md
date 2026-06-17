# Organization Dashboard

ระบบสารพัดประโยชน์สำหรับองค์กร สำหรับแจ้งรายงาน ติดตามเหตุการณ์ และจัดการข้อมูลผู้ใช้

## Tech Stack

- **Framework**: ASP.Net Core MVC (.NET 10.0)
- **Language**: C#
- **Database**: MySQL
- **Styling**: Bootstrap 5 + Custom CSS
- **Frontend**: JavaScript / jQuery

## Features

### 1. Dashboard
- ดูภาพรวมโปรเจค (Projects)
- รายงานล่าสุด (Recent Reports)
- เหตุการณ์ที่กำลังจะเกิดขึ้น (Upcoming Events)

### 2. Reporting System
- ส่งรายงานปัญหา (Computer repair, broken light, internet issues, leave requests)
- ระบุลำดับความจำเป็น (Low, Medium, High, Urgent)
- เลือกวันเวลาและสถานที่
- แนบรูปภาพ
- มอบหมายให้บุคคลหรือหลายบุคคล
- ติดตามสถานะ (Pending, InProgress, Resolved, Closed)
- ความเห็นและอธิบายเพิ่มเติม

### 3. Event Calendar
- สร้างและจัดการเหตุการณ์องค์กร
- ปักหมุดเหตุการณ์ตามวันเวลา
- ตรวจสอบตัวแทนของเหตุการณ์

### 4. User Management
- ข้อมูลประวัติผู้ใช้ (FirstName, LastName, Nickname)
- ข้อมูลติดต่อ (Email, Phone, Line)
- ตำแหน่ง (Position, Role)
- สถานที่ทำงาน (Floor, Desk)
- สถานะ (Available, Unavailable, Absent)
- ลิงค์อื่น (GitHub, Portfolio)

### 5. Admin Dashboard
- สร้างบัญชีผู้ใช้ (สำหรับ Admin เท่านั้น)
- ตั้งค่า username/password โดยผ่าน Admin Panel

### 6. Authentication
- ระบบ Login/Logout
- Password hashing ด้วย BCrypt
- Session management

## Database Structure

### Tables
- **Users** - ข้อมูลผู้ใช้ระบบ
- **Reports** - รายงานปัญหา/การขอบริการ
- **ReportAssignments** - การมอบหมายรายงาน
- **ReportComments** - ความเห็นต่อรายงาน
- **OrganizationEvents** - เหตุการณ์องค์กร
- **EventAttendees** - ผู้เข้าร่วมเหตุการณ์
- **Projects** - โปรเจค/งาน

## Installation & Setup

### Prerequisites
- .NET 10.0 SDK
- MySQL Server 8.0 or higher
- Visual Studio 2022 or VS Code

### Steps

1. **Clone the project**
   ```bash
   git clone <repository-url>
   cd WebApplication1
   ```

2. **Configure Database Connection**
   - Edit `WebApplication1/appsettings.json`
   - Update connection string:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Port=3306;Database=OrganizationDashboard;User=root;Password=YourPassword;"
   }
   ```

3. **Create Database**
   ```bash
   cd WebApplication1/WebApplication1
   dotnet ef database update
   ```

4. **Install NuGet Packages** (if not auto-restored)
   ```bash
   dotnet restore
   ```

5. **Run the Application**
   ```bash
   dotnet run
   ```
   - Application will run on: `https://localhost:5001` or `http://localhost:5000`

## Default Login

After creating the database, you need to create your first admin user through the Register page or SQL insert:

```sql
INSERT INTO Users (FirstName, LastName, Username, PasswordHash, Email, Role, IsActive, CreatedAt)
VALUES ('Admin', 'User', 'admin', 'BCrypt_Hash_Here', 'admin@org.com', 'Admin', true, NOW());
```

## Project Structure

```
WebApplication1/
├── Controllers/
│   ├── AuthController.cs
│   ├── DashboardController.cs
│   ├── EventController.cs
│   ├── ReportController.cs
│   └── UserController.cs
├── Data/
│   └── ApplicationDbContext.cs
├── Models/
│   ├── User.cs
│   ├── Report.cs
│   ├── ReportAssignment.cs
│   ├── ReportComment.cs
│   ├── OrganizationEvent.cs
│   ├── EventAttendee.cs
│   └── Project.cs
├── Views/
│   ├── Auth/
│   ├── Dashboard/
│   ├── Report/
│   ├── Event/
│   ├── User/
│   └── Shared/
├── wwwroot/
│   ├── css/
│   ├── js/
│   └── lib/
├── Program.cs
├── appsettings.json
└── WebApplication1.csproj
```

## Usage

### 1. Login
- Navigate to `/Auth/Login`
- Enter username and password

### 2. Create Report
- Click "Reports" in navigation
- Click "Create New Report"
- Fill in title, description, priority, date/time, location
- Assign to user(s)
- Add comments/attachments

### 3. Create Event
- Click "Events" in navigation
- Click "Create New Event"
- Fill in event details (title, date/time, location)
- Event will appear in calendar

### 4. Manage Users (Admin Only)
- Click "Users" in navigation
- Create new user account (admin endpoint)
- Edit user profile and permissions

## Security Notes

- Passwords are hashed using BCrypt.Net-Next
- Session timeout is set to 30 minutes
- HTTPS is enforced in production
- CSRF protection enabled on all forms
- User registration disabled for non-admin users

## Future Enhancements

- [x] Multi-file upload for reports
- [ ] Email notifications for report updates
- [ ] Advanced filtering and search
- [ ] Report analytics dashboard
- [ ] Mobile app
- [ ] Real-time notifications
- [ ] Photo gallery with report attachments

## Contributing

This is an internal organizational tool. Contact the development team for contributions.

## License

Internal use only.
