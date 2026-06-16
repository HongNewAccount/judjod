-- Insert Test Users
INSERT INTO Users (FirstName, LastName, NickName, Username, PasswordHash, Email, Phone, Line, Role, Position, WorkLocation, Status, IsActive, CreatedAt) VALUES
('Admin', 'User', 'Admin', 'admin', '$2a$11$8c5vfj3k9mXw8cV9pL2p4ewKkK9j7mC9gK3z5vV8n9X9L2p4q3w5', 'admin@organization.com', '081-000-0000', 'admin_line', 'Admin', 'Administrator', 'Floor 1, Desk 1', 'Available', 1, NOW()),
('Somchai', 'Developer', 'Som', 'somchai', '$2a$11$8c5vfj3k9mXw8cV9pL2p4ewKkK9j7mC9gK3z5vV8n9X9L2p4q3w5', 'somchai@organization.com', '081-111-1111', 'som_dev', 'Manager', 'Project Manager', 'Floor 2, Desk 5', 'Available', 1, NOW()),
('Niran', 'Engineer', 'Niran', 'niran', '$2a$11$8c5vfj3k9mXw8cV9pL2p4ewKkK9j7mC9gK3z5vV8n9X9L2p4q3w5', 'niran@organization.com', '081-222-2222', 'niran_eng', 'User', 'Software Engineer', 'Floor 2, Desk 10', 'Available', 1, NOW()),
('Wilai', 'Designer', 'Wilai', 'wilai', '$2a$11$8c5vfj3k9mXw8cV9pL2p4ewKkK9j7mC9gK3z5vV8n9X9L2p4q3w5', 'wilai@organization.com', '081-333-3333', 'wilai_design', 'User', 'UI/UX Designer', 'Floor 3, Desk 2', 'Available', 1, NOW());

-- Insert Test Projects
INSERT INTO Projects (Name, Description, Owner, StartDate, EndDate, Status, Issues, CreatedByUserId, CreatedAt) VALUES
('Website Redesign 2026', 'Complete redesign of company website with new UI/UX', 'Somchai Developer', '2026-01-15', '2026-06-30', 'InProgress', 'Waiting for design approval from management', 1, DATE_SUB(NOW(), INTERVAL 30 DAY)),
('Mobile App Development', 'Develop iOS and Android apps for customer portal', 'Niran Engineer', '2026-02-01', '2026-08-31', 'InProgress', 'Need additional resources for testing phase', 1, DATE_SUB(NOW(), INTERVAL 25 DAY)),
('Database Migration', 'Migrate legacy database to cloud infrastructure', 'Somchai Developer', '2026-03-01', '2026-05-31', 'Planning', NULL, 1, DATE_SUB(NOW(), INTERVAL 20 DAY)),
('Security Audit', 'Full security assessment and penetration testing', 'Admin User', '2026-01-01', '2026-04-15', 'Completed', 'Minor vulnerabilities found and patched', 1, DATE_SUB(NOW(), INTERVAL 60 DAY)),
('API Integration', 'Integrate third-party payment and analytics APIs', 'Niran Engineer', '2026-04-01', '2026-06-15', 'OnHold', 'Waiting for vendor API documentation update', 1, DATE_SUB(NOW(), INTERVAL 15 DAY)),
('Email Marketing System', 'Build automated email marketing platform', 'Somchai Developer', '2026-05-01', '2026-07-30', 'Planning', NULL, 1, DATE_SUB(NOW(), INTERVAL 10 DAY));

-- Insert Test Reports
INSERT INTO Reports (Title, Description, Priority, Status, ReportedDate, ScheduledDate, Location, IsOnSite, CreatedByUserId, CreatedAt) VALUES
('Network Connectivity Issue', 'Internet connection drops frequently on Floor 2. Network performance is affecting productivity.', 'High', 'InProgress', DATE_SUB(NOW(), INTERVAL 5 DAY), DATE_ADD(NOW(), INTERVAL 1 DAY), 'Floor 2, Conference Room', 1, 2, DATE_SUB(NOW(), INTERVAL 5 DAY)),
('Computer Hardware Failure', 'Desktop computer in Dev Lab crashes randomly. May need hardware replacement.', 'Medium', 'Pending', DATE_SUB(NOW(), INTERVAL 3 DAY), DATE_ADD(NOW(), INTERVAL 2 DAY), 'Floor 2, Dev Lab', 1, 3, DATE_SUB(NOW(), INTERVAL 3 DAY)),
('Leave Request', 'Request for annual leave on June 20-24, 2026 for family vacation', 'Low', 'Pending', DATE_SUB(NOW(), INTERVAL 2 DAY), '2026-06-20', 'Out of office', 0, 4, DATE_SUB(NOW(), INTERVAL 2 DAY)),
('Broken Ceiling Light', 'Fluorescent light fixture on Floor 3 is flickering and needs to be replaced', 'Low', 'Resolved', DATE_SUB(NOW(), INTERVAL 10 DAY), DATE_SUB(NOW(), INTERVAL 9 DAY), 'Floor 3, Corridor', 1, 2, DATE_SUB(NOW(), INTERVAL 10 DAY));

-- Insert Test Events
INSERT INTO OrganizationEvents (Title, Description, EventDate, EventEndDate, Location, IsOnSite, CreatedByUserId, CreatedAt) VALUES
('Team Lunch & Networking', 'Monthly team gathering for lunch and casual networking. All staff welcome!', '2026-06-20 12:00:00', '2026-06-20 13:30:00', 'Main Cafeteria', 1, 1, DATE_SUB(NOW(), INTERVAL 5 DAY)),
('Quarterly All-Hands Meeting', 'CEO will present quarterly results and discuss company strategy for next quarter', '2026-06-25 10:00:00', '2026-06-25 11:30:00', 'Auditorium', 1, 1, DATE_SUB(NOW(), INTERVAL 10 DAY)),
('Training: ASP.NET Core Advanced', 'Professional development workshop on advanced ASP.NET Core topics including performance optimization', '2026-06-27 14:00:00', '2026-06-27 17:00:00', 'Training Room B', 1, 2, DATE_SUB(NOW(), INTERVAL 8 DAY)),
('Company Anniversary Party', 'Celebrate company''s 10th anniversary with food, entertainment, and awards ceremony', '2026-07-15 18:00:00', '2026-07-15 22:00:00', 'Grand Ballroom Hotel', 0, 1, DATE_SUB(NOW(), INTERVAL 15 DAY));
