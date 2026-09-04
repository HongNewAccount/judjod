# JUDJOD — Organization Dashboard

ระบบจัดการงานและการสื่อสารภายในองค์กร

## Tech Stack

- **Framework**: ASP.NET Core MVC (.NET 10.0)
- **Language**: C#
- **Database**: MySQL 8.0
- **ORM**: Entity Framework Core
- **Styling**: Bootstrap 5 + Custom CSS
- **Frontend**: Vanilla JavaScript
- **Auth**: BCrypt.Net-Next + Session

## Features

### 1. Tasks (Kanban Board)
- แสดงงานแบบ Kanban (Planning → InProgress → OnHold → Completed)
- กรองหลายเงื่อนไขพร้อมกัน (ชื่อ, Priority, Group, Due Date, Progress)
- เรียงลำดับ (Name / Priority / Due Date / Progress) แบบ ↑ ↓
- ระบบอนุมัติสร้างงาน (Admin/Editor)
- ติดตาม Progress พร้อม log การเปลี่ยนแปลง

### 2. Archive
- งานที่ปิดแล้ว (Status = Closed)
- กรองและเรียงลำดับเหมือนหน้า Tasks
- Reopen งานกลับมาได้

### 3. Notes
- กระดานโน้ตส่วนกลาง ทุกคนเห็นโน้ตของทุกคน
- สร้าง / แก้ไข / ลบโน้ต
- โน้ตหมดอายุอัตโนมัติใน 30 วัน
- แจ้งเตือน badge เมื่อมีโน้ตใหม่จากคนอื่น

### 4. Chat
- ห้องแชทแบบ Group และ Direct Message
- รองรับรูปภาพ
- แจ้งเตือน badge เมื่อมีข้อความที่ยังไม่อ่าน

### 5. Calendar
- ดูภาพรวมกิจกรรมและ deadline ของทีม

### 6. User Management
- ดูโปรไฟล์และงานที่รับผิดชอบของแต่ละคน
- แก้ไขโปรไฟล์ตัวเอง (ชื่อ, Username, รหัสผ่าน, รูป)
- Admin: จัดการ Role, ระงับสิทธิ์, อนุมัติการสมัคร

### 7. Activity Log
- บันทึกทุก action ในระบบ (สร้าง/แก้ไข/ลบงาน, เปลี่ยน password ฯลฯ)

### 8. REST API (JudjodApi)
- โปรเจคแยกสำหรับ external access
- ยืนยันตัวตนด้วย API Key

## Installation & Setup

### Prerequisites
- .NET 10.0 SDK
- MySQL Server 8.0+

### Steps

1. **Clone the project**
   ```bash
   git clone <repository-url>
   cd judjod/WebApplication1/WebApplication1
   ```

2. **Configure database**

   แก้ `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Port=3306;Database=OrganizationDashboard;User=root;Password=YourPassword;"
   }
   ```

3. **Run** (ตาราง DB สร้างอัตโนมัติเมื่อ start)
   ```bash
   dotnet run
   ```
   App จะรันที่ `http://localhost:5000`

### Default Admin Account

สร้าง admin คนแรกผ่าน `/Auth/Register` แล้ว insert SQL เพื่อ set Role:
```sql
UPDATE Users SET Role = 'Admin', IsActive = 1, PendingApproval = 0 WHERE Username = 'your_username';
```

## Database Tables

| Table | คำอธิบาย |
|---|---|
| Users | ผู้ใช้ระบบ |
| Projects | งาน/โปรเจค |
| ProjectOwners | ความสัมพันธ์ผู้รับผิดชอบ-งาน |
| ProjectGroups | กลุ่ม/ทีม |
| ProjectGroupAssignments | ความสัมพันธ์งาน-กลุ่ม |
| ProjectApprovalRequests | คำขออนุมัติสร้างงาน |
| ProjectProgressLogs | log การอัพเดต progress |
| StickyNotes | โน้ต |
| ChatRooms | ห้องแชท |
| ChatRoomMembers | สมาชิกห้องแชท |
| ChatRoomMessages | ข้อความแชท |
| ActivityLogs | log ทุก action |

## Project Structure

```
WebApplication1/
├── Controllers/
│   ├── AuthController.cs
│   ├── ProjectTrackerController.cs
│   ├── StickyNoteController.cs
│   ├── ChatController.cs
│   ├── UserController.cs
│   ├── DashboardController.cs
│   └── ApiController.cs
├── Data/
│   └── ApplicationDbContext.cs
├── Models/
│   ├── User.cs
│   ├── Project.cs
│   ├── ProjectGroup.cs
│   ├── StickyNote.cs
│   ├── ChatRoom.cs
│   ├── ChatRoomMessage.cs
│   └── ActivityLog.cs
├── Views/
│   ├── Auth/
│   ├── ProjectTracker/
│   ├── StickyNote/
│   ├── Chat/
│   ├── User/
│   ├── Dashboard/
│   └── Shared/
├── wwwroot/
│   ├── css/
│   ├── js/
│   └── uploads/
├── Program.cs
└── appsettings.json
```

## Security

- Password hashing ด้วย BCrypt
- Session timeout 30 นาที
- CSRF protection ทุก form
- ระบบ Role: Admin / Editor / User

## License

Internal use only.
