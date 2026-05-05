PRAGMA foreign_keys = OFF;

DROP TABLE IF EXISTS task;
DROP TABLE IF EXISTS project;
DROP TABLE IF EXISTS person;
DROP TABLE IF EXISTS department;

PRAGMA foreign_keys = ON;

CREATE TABLE department (
    department_id INTEGER PRIMARY KEY,
    name VARCHAR(64) NOT NULL COLLATE NOCASE,
    region VARCHAR(32) NOT NULL COLLATE NOCASE,
    budget REAL NOT NULL,
    active INTEGER NOT NULL,
    created_utc DATETIME NOT NULL
);

CREATE TABLE person (
    person_id INTEGER PRIMARY KEY,
    department_id INTEGER NOT NULL,
    first_name VARCHAR(64) NOT NULL COLLATE NOCASE,
    last_name VARCHAR(64) NOT NULL COLLATE NOCASE,
    email VARCHAR(128) NOT NULL COLLATE NOCASE,
    title VARCHAR(64) NOT NULL COLLATE NOCASE,
    hire_date DATETIME NOT NULL,
    active INTEGER NOT NULL,
    FOREIGN KEY (department_id) REFERENCES department(department_id)
);

CREATE TABLE project (
    project_id INTEGER PRIMARY KEY,
    department_id INTEGER NOT NULL,
    name VARCHAR(96) NOT NULL COLLATE NOCASE,
    status VARCHAR(32) NOT NULL COLLATE NOCASE,
    budget REAL NOT NULL,
    kickoff_utc DATETIME NOT NULL,
    target_utc DATETIME NOT NULL,
    FOREIGN KEY (department_id) REFERENCES department(department_id)
);

CREATE TABLE task (
    task_id INTEGER PRIMARY KEY,
    project_id INTEGER NOT NULL,
    assignee_person_id INTEGER NOT NULL,
    title VARCHAR(128) NOT NULL COLLATE NOCASE,
    status VARCHAR(32) NOT NULL COLLATE NOCASE,
    priority VARCHAR(16) NOT NULL COLLATE NOCASE,
    estimate_hours REAL NOT NULL,
    due_utc DATETIME NOT NULL,
    completed INTEGER NOT NULL,
    FOREIGN KEY (project_id) REFERENCES project(project_id),
    FOREIGN KEY (assignee_person_id) REFERENCES person(person_id)
);

INSERT INTO department (department_id, name, region, budget, active, created_utc) VALUES
    (1, 'Platform', 'North America', 2500000.00, 1, '2024-01-04 09:00:00'),
    (2, 'Customer Success', 'Europe', 1450000.00, 1, '2024-01-08 09:30:00'),
    (3, 'Research', 'North America', 1800000.00, 1, '2024-01-15 10:00:00'),
    (4, 'Operations', 'APAC', 980000.00, 1, '2024-01-22 08:45:00');

INSERT INTO person (person_id, department_id, first_name, last_name, email, title, hire_date, active) VALUES
    (1, 1, 'Maya', 'Patel', 'maya.patel@example.com', 'Director', '2021-03-01 09:00:00', 1),
    (2, 1, 'Lucas', 'Nguyen', 'lucas.nguyen@example.com', 'Staff Engineer', '2022-06-13 09:00:00', 1),
    (3, 2, 'Elena', 'Fischer', 'elena.fischer@example.com', 'Program Lead', '2020-11-09 09:00:00', 1),
    (4, 2, 'Jordan', 'Carter', 'jordan.carter@example.com', 'Success Manager', '2023-01-17 09:00:00', 1),
    (5, 3, 'Noah', 'Kim', 'noah.kim@example.com', 'Research Lead', '2019-09-23 09:00:00', 1),
    (6, 3, 'Priya', 'Shah', 'priya.shah@example.com', 'Analyst', '2024-02-12 09:00:00', 1),
    (7, 4, 'Isla', 'Murphy', 'isla.murphy@example.com', 'Ops Manager', '2021-07-19 09:00:00', 1),
    (8, 4, 'Diego', 'Romero', 'diego.romero@example.com', 'Coordinator', '2024-04-02 09:00:00', 1);

INSERT INTO project (project_id, department_id, name, status, budget, kickoff_utc, target_utc) VALUES
    (1, 1, 'Edge Gateway Refresh', 'Active', 620000.00, '2025-01-06 09:00:00', '2025-06-30 17:00:00'),
    (2, 1, 'Tenant Audit Trail', 'Planning', 410000.00, '2025-02-10 09:00:00', '2025-08-15 17:00:00'),
    (3, 2, 'Executive Onboarding Sprint', 'Active', 225000.00, '2025-01-20 09:00:00', '2025-04-25 17:00:00'),
    (4, 3, 'Retention Signal Model', 'Active', 830000.00, '2025-01-13 09:00:00', '2025-09-12 17:00:00'),
    (5, 3, 'Forecast Workbook Cleanup', 'Paused', 160000.00, '2024-11-11 09:00:00', '2025-03-21 17:00:00'),
    (6, 4, 'Quarterly Incident Drill', 'Active', 120000.00, '2025-03-03 09:00:00', '2025-05-30 17:00:00');

INSERT INTO task (task_id, project_id, assignee_person_id, title, status, priority, estimate_hours, due_utc, completed) VALUES
    (1, 1, 2, 'Finalize edge routing schema', 'In Progress', 'High', 18.5, '2025-05-07 17:00:00', 0),
    (2, 1, 1, 'Approve rollout checklist', 'Blocked', 'High', 4.0, '2025-05-08 17:00:00', 0),
    (3, 2, 2, 'Draft tenancy retention policy', 'Queued', 'Medium', 9.0, '2025-05-21 17:00:00', 0),
    (4, 2, 1, 'Review audit event naming', 'Queued', 'Low', 3.5, '2025-05-23 17:00:00', 0),
    (5, 3, 3, 'Publish onboarding packet', 'Done', 'High', 6.0, '2025-04-01 17:00:00', 1),
    (6, 3, 4, 'Validate stakeholder contact map', 'In Progress', 'Medium', 7.5, '2025-05-10 17:00:00', 0),
    (7, 4, 5, 'Prepare model training slice', 'Done', 'High', 14.0, '2025-03-15 17:00:00', 1),
    (8, 4, 6, 'Review false-positive cohort', 'In Progress', 'High', 11.0, '2025-05-14 17:00:00', 0),
    (9, 4, 5, 'Write experiment summary', 'Queued', 'Medium', 5.0, '2025-05-20 17:00:00', 0),
    (10, 5, 6, 'Normalize workbook dimensions', 'Paused', 'Low', 8.5, '2025-04-18 17:00:00', 0),
    (11, 6, 7, 'Publish drill timeline', 'In Progress', 'Medium', 6.5, '2025-05-09 17:00:00', 0),
    (12, 6, 8, 'Confirm APAC responder roster', 'Queued', 'High', 4.5, '2025-05-12 17:00:00', 0);
