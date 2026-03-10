# Proposed EF Diagram

This proposes a normalized Entity Framework data model to support the current frontend behavior:

- admins can manage teachers and other admins
- teachers can manage only their own classes
- classes have many teachers and many students
- teachers assign scenarios to classes
- students submit work for assignments

```mermaid
erDiagram

    USERS {
        int id PK
        string sso_subject UK
        string email UK
        string name
        string role "admin | teacher | student"
        datetime created_at_utc
    }

    CLASSES {
        int id PK
        string name UK
        datetime created_at_utc
        int created_by_user_id FK
    }

    CLASS_TEACHERS {
        int id PK
        int class_id FK
        int teacher_user_id FK
        datetime added_at_utc
        int added_by_user_id FK
    }

    CLASS_STUDENTS {
        int id PK
        int class_id FK
        int student_user_id FK
        datetime added_at_utc
        int added_by_user_id FK
    }

    SCENARIOS {
        string id PK
        string title
        string description
        string created_by_email
        json nodes_json
        datetime created_at_utc
    }

    ASSIGNMENTS {
        int id PK
        int class_id FK
        string scenario_id FK
        string name
        datetime assigned_at_utc
        datetime due_at_utc
        int assigned_by_user_id FK
    }

    SUBMISSIONS {
        int id PK
        int assignment_id FK
        int student_user_id FK
        datetime submitted_at_utc
        string submission_text
        decimal grade
        string grade_feedback
        datetime graded_at_utc
        int graded_by_user_id FK
    }

    USERS ||--o{ CLASS_TEACHERS : "teaches"
    USERS ||--o{ CLASS_STUDENTS : "enrolled in"
    USERS ||--o{ CLASSES : "creates"
    USERS ||--o{ ASSIGNMENTS : "assigns"
    USERS ||--o{ SUBMISSIONS : "submits"
    USERS ||--o{ SUBMISSIONS : "grades"

    CLASSES ||--o{ CLASS_TEACHERS : "has teachers"
    CLASSES ||--o{ CLASS_STUDENTS : "has students"
    CLASSES ||--o{ ASSIGNMENTS : "has assignments"

    SCENARIOS ||--o{ ASSIGNMENTS : "used by"

    ASSIGNMENTS ||--o{ SUBMISSIONS : "receives"
```

## Notes

- `USERS.role` handles platform-level access: `admin`, `teacher`, `student`.
- Class ownership and visibility should come from `CLASS_TEACHERS`, not from the global role alone.
- `CLASS_TEACHERS` allows multiple teachers on one class.
- `CLASS_STUDENTS` replaces storing student emails directly on the class record.
- `ASSIGNMENTS` should be a real table instead of JSON embedded in class data.
- `SUBMISSIONS` should belong to a specific assignment, which avoids ambiguity when the same scenario is assigned more than once.

## Recommended EF navigation shape

- `User`
  - `CreatedClasses`
  - `TeachingAssignments`
  - `StudentEnrollments`
  - `AssignedAssignments`
  - `SubmittedAssignments`
  - `GradedSubmissions`
- `Class`
  - `CreatedBy`
  - `Teachers`
  - `Students`
  - `Assignments`
- `Assignment`
  - `Class`
  - `Scenario`
  - `AssignedBy`
  - `Submissions`
- `Submission`
  - `Assignment`
  - `Student`
  - `GradedBy`
