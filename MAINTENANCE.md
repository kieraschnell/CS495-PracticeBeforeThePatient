# Practice Before The Patient — Maintenance Documentation

This document provides detailed information for developers, system administrators, and maintainers who need to install, deploy, extend, or troubleshoot the Practice Before The Patient application.

---

## Table of Contents

1. [Installation & Deployment](#installation--deployment)
   - [Prerequisites](#prerequisites)
   - [Local Development Setup](#local-development-setup)
   - [Production Deployment](#production-deployment)
2. [Feature Documentation](#feature-documentation)
   - [User Roles & Access Control](#user-roles--access-control)
   - [Simulation Runner](#simulation-runner)
   - [Scenario Editor](#scenario-editor)
   - [Class Management](#class-management)
   - [Assignment & Grading System](#assignment--grading-system)
   - [Admin Management](#admin-management)
3. [Modifying & Extending the Software](#modifying--extending-the-software)
   - [Technology Stack](#technology-stack)
   - [Build & Dependency Management](#build--dependency-management)
   - [Database Migrations](#database-migrations)
   - [Code Style & Conventions](#code-style--conventions)
   - [Adding New Features](#adding-new-features)
4. [Testing](#testing)
5. [Backlog & Known Issues](#backlog--known-issues)
6. [FAQs](#faqs)

---

## Installation & Deployment

### Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| .NET SDK | 9.0+ | [Download](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Visual Studio 2022 | 17.12+ | With **ASP.NET and web development** workload |
| SQLite | Built-in | No separate installation needed |
| Git | Latest | For version control |
| Node.js | Optional | Only if extending frontend tooling |

**Operating System**: Windows 10+, macOS, or Linux (for production servers)

### Local Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/kieraschnell/CS495-PracticeBeforeThePatient.git
   cd CS495-PracticeBeforeThePatient
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Apply database migrations**
   ```bash
   dotnet ef database update --project PracticeBeforeThePatient.Api --startup-project PracticeBeforeThePatient.Api
   ```

4. **Run both projects**

   **Option A: Visual Studio (Recommended)**
   - Open `CS495-PracticeBeforeThePatient.sln`
   - Right-click Solution → **Configure Startup Projects**
   - Select **Multiple startup projects**
   - Set both `PracticeBeforeThePatient.Api` and `PracticeBeforeThePatient.Web` to **Start**
   - Press **F5**

   **Option B: Command Line**
   ```bash
   # Terminal 1 - API
   dotnet run --project PracticeBeforeThePatient.Api --launch-profile https

   # Terminal 2 - Web
   dotnet run --project PracticeBeforeThePatient.Web --launch-profile https
   ```

5. **Access the application**
   | Service | URL |
   |---------|-----|
   | Web UI | https://localhost:7124 |
   | API | https://localhost:7144 |
   | Swagger UI | https://localhost:7144/swagger |

### Production Deployment

#### GCP Compute Engine (Current Setup)

The application is deployed to a GCP Compute Engine VM with:
- **Caddy** reverse proxy (auto-HTTPS via Let's Encrypt)
- **systemd** services for API and Web
- **GitHub Actions** CI/CD (auto-deploy on push to `main`)

**Manual Deployment Steps:**

1. **SSH into the VM**
   ```bash
   ssh <username>@<vm-ip>
   ```

2. **Navigate to application directory**
   ```bash
   cd ~/CS495-PracticeBeforeThePatient
   ```

3. **Pull latest changes**
   ```bash
   git pull origin main
   ```

4. **Publish applications**
   ```bash
   sudo dotnet publish PracticeBeforeThePatient.Api -c Release -o /var/www/pbtp-api
   sudo dotnet publish PracticeBeforeThePatient.Web -c Release -o /var/www/pbtp-web
   ```

5. **Restart services**
   ```bash
   sudo systemctl restart pbtp-api
   sudo systemctl restart pbtp-web
   ```

6. **Check service status**
   ```bash
   sudo systemctl status pbtp-api
   sudo systemctl status pbtp-web
   ```

**Viewing Logs:**
```bash
sudo journalctl -u pbtp-api -n 100 --no-pager
sudo journalctl -u pbtp-web -n 100 --no-pager
```

#### Environment Variables / Configuration

| Setting | Project | Dev Value | Prod Value |
|---------|---------|-----------|------------|
| `ASPNETCORE_ENVIRONMENT` | Both | `Development` | `Production` |
| `ApiBaseUrl` | Web | `http://localhost:5186/` | `http://localhost:5100/` |

Configuration files:
- `appsettings.json` — base settings
- `appsettings.Development.json` — development overrides
- `appsettings.Production.json` — production overrides


## Feature Documentation

### User Roles & Access Control

The system supports three user roles with hierarchical permissions:

| Role | Capabilities |
|------|--------------|
| **Admin** | Full system access: manage users, all classes, all scenarios, all assignments |
| **Teacher** | Create/edit scenarios, manage assigned classes, create assignments, grade submissions |
| **Student** | Complete assigned simulations, view grades, submit assignments |

**How roles are determined:**
- Stored in `Users.Role` column in database
- Roles: `admin`, `teacher`, `student`
- Dev mode allows switching users via Settings page

**Feature flow:**
1. User accesses `/api/access` → returns role, permissions, allowed scenarios
2. NavMenu conditionally shows links based on role
3. Each page validates access on load

### Simulation Runner

**Location:** `PracticeBeforeThePatient.Web/Components/Pages/Simulation.razor`

**How to use:**
1. Navigate to **Simulation** from sidebar
2. Select an available assignment from dropdown
3. Read scenario prompts and select choices
4. Provide reasoning for each decision (optional)
5. Click **Submit** when complete

**Data flow:**
1. `GET /api/access` → retrieves allowed assignments/scenarios for current user
2. `GET /api/scenarios/{id}` → loads scenario content (nodes/choices)
3. `POST /api/assignments/submit-scenario` → submits completion with reasoning text
4. Creates/updates `Submissions` record linked to assignment and student

**Links to other features:**
- Submitted assignments appear on **Grades** page
- Teachers can grade submissions via **Assignment Grading** page

### Scenario Editor

**Location:** `PracticeBeforeThePatient.Web/Components/Pages/ScenarioEditor.razor`

**Access:** Teachers and Admins only

**How to use:**
1. Navigate to **Scenario Editor** from sidebar
2. Select existing scenario from dropdown
3. Edit scenario title and description
4. Expand/collapse nodes using tree controls
5. Modify node content, choices, and branching
6. Click **Save Changes**

**Data flow:**
1. `GET /api/scenarios` → lists all scenario IDs
2. `GET /api/scenarios/{id}` → loads full scenario JSON
3. `PUT /api/scenarios/{id}` → saves modified scenario

**Validation rules:**
- Scenario must have a title
- MCQ nodes must have at least one choice
- Each choice must have a label and text

**Database storage:**
- Scenarios stored in `Scenarios` table
- `NodesJson` column contains serialized decision tree

### Class Management

**Location:** `PracticeBeforeThePatient.Web/Components/Pages/ClassManagement.razor`

**Access:** Teachers and Admins

**How to use:**
1. Navigate to **Class Management** from sidebar
2. Create new class via **Add class** button
3. Select class to manage from dropdown
4. Add/remove teachers and students by email
5. Create assignments linking scenarios to class
6. Set due dates for assignments

**Data flow:**
- Classes → `Classes` table
- Teacher assignments → `ClassTeachers` table
- Student enrollments → `ClassStudents` table
- Assignments → `Assignments` table

**Feature interactions:**
- Adding student creates `Users` record if email doesn't exist
- Assignments appear in student's simulation dropdown
- Deleting class cascades to remove teachers, students, assignments, submissions

### Assignment & Grading System

**Location:** `PracticeBeforeThePatient.Web/Components/Pages/AssignmentGrading.razor`

**Access:** Teachers and Admins (for classes they teach)

**How to use:**
1. From **Class Management**, click **Grade** on an assignment
2. View submission status for each enrolled student
3. Click student row to expand submission details
4. Enter grade (0-100) and optional feedback
5. Click **Save grade**

**Submission states:**
| State | Meaning |
|-------|---------|
| Not submitted | Student hasn't completed simulation |
| Submitted | Awaiting grade |
| Graded | Has grade and optional feedback |

**Data flow:**
- `GET /api/classes/{id}/assignments` → lists assignments with submissions
- `PUT /api/classes/{id}/assignments/{id}/grades` → saves grade

### Admin Management

**Location:** `PracticeBeforeThePatient.Web/Components/Pages/AdminManagement.razor`

**Access:** Admins only

**How to use:**
1. Navigate to **Admin Management** from sidebar
2. View all users with elevated access (teachers/admins)
3. Add new teacher/admin via **Add teacher/admin** button
4. Edit existing user's role
5. Remove elevated access (demotes to student)

**Notes:**
- Cannot remove your own admin access
- Creating teacher/admin creates user record if needed
- Students are added via Class Management, not here

---

## Modifying & Extending the Software

### Technology Stack

| Component | Technology |
|-----------|------------|
| **Frontend** | Blazor Server (.NET 9) |
| **Backend** | ASP.NET Core Web API (.NET 9) |
| **Database** | SQLite with Entity Framework Core 9 |
| **Shared Models** | .NET 9 Class Library |
| **Styling** | CSS (custom, no framework) |
| **Deployment** | systemd + Caddy on GCP VM |
| **CI/CD** | GitHub Actions |

### Build & Dependency Management

**Build Commands:**
```bash
# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Publish for production
dotnet publish -c Release
```

**Dependencies are listed in:**
- `PracticeBeforeThePatient.Api/PracticeBeforeThePatient.Api.csproj`
- `PracticeBeforeThePatient.Web/PracticeBeforeThePatient.Web.csproj`
- `PracticeBeforeThePatient.Core/PracticeBeforeThePatient.Core.csproj`

**Key NuGet packages:**
| Package | Project | Purpose |
|---------|---------|---------|
| `Microsoft.EntityFrameworkCore.Sqlite` | API | Database provider |
| `Microsoft.EntityFrameworkCore.Design` | API | Migration tooling |
| `Swashbuckle.AspNetCore` | API | Swagger/OpenAPI |

**Adding packages:**
```bash
dotnet add PracticeBeforeThePatient.Api package <PackageName>
```

### Database Migrations

The project uses EF Core migrations for schema management.

**Creating a new migration:**
```bash
dotnet ef migrations add <MigrationName> \
  --project PracticeBeforeThePatient.Api \
  --startup-project PracticeBeforeThePatient.Api
```

**Applying migrations:**
```bash
dotnet ef database update \
  --project PracticeBeforeThePatient.Api \
  --startup-project PracticeBeforeThePatient.Api
```

**Listing migrations:**
```bash
dotnet ef migrations list \
  --project PracticeBeforeThePatient.Api \
  --startup-project PracticeBeforeThePatient.Api
```

**Removing last migration (if not applied):**
```bash
dotnet ef migrations remove \
  --project PracticeBeforeThePatient.Api \
  --startup-project PracticeBeforeThePatient.Api
```

**Database location:** `PracticeBeforeThePatient.Api/Data/app.db`

**Resetting database:**
```bash
rm PracticeBeforeThePatient.Api/Data/app.db
dotnet ef database update --project PracticeBeforeThePatient.Api --startup-project PracticeBeforeThePatient.Api
```

### Code Style & Conventions

**General:**
- C# 13.0 with nullable reference types enabled
- Implicit usings enabled
- 4-space indentation
- PascalCase for public members, _camelCase for private fields
- Async/await for all I/O operations

**Blazor components:**
- `.razor` for markup, `.razor.cs` for code-behind
- Use `[Inject]` for dependency injection
- Prefer `protected` for fields used in markup

**API Controllers:**
- One controller per resource/domain
- Return `ActionResult<T>` for typed responses
- Use `[FromBody]`, `[FromRoute]` attributes explicitly
- Return appropriate status codes (200, 400, 403, 404)

**Entity Framework:**
- Configure relationships in `AppDbContext.OnModelCreating`
- Use navigation properties for relationships
- Use `.AsNoTracking()` for read-only queries

### Adding New Features

1. **Plan the data model**
   - Add entity classes in `PracticeBeforeThePatient.Api/Data/Entities/`
   - Update `AppDbContext` with DbSet and configuration
   - Create migration

2. **Add API endpoints**
   - Create or update controller in `Controllers/`
   - Add DTOs for request/response
   - Implement authorization checks

3. **Update shared models (if needed)**
   - Add to `PracticeBeforeThePatient.Core/Models/`

4. **Add UI components**
   - Create Blazor component in `Components/Pages/`
   - Add navigation link in `NavMenu.razor`
   - Use `ApiClient` for API calls
   - Check that UI elements are [WCAG 2.1 AA](https://www.w3.org/TR/WCAG21/) compliant

5. **Test and document**
   - Test all user roles
   - Update this documentation


Develpoment of this project was done in [Visual Studio](https://visualstudio.microsoft.com/), which should be noted as distinct 
from [Visual Studio Code](https://code.visualstudio.com/). When editing the project as a whole, Visual Studio should be used. 
Individual code files can be edited with different code editors, but Visual Studio should be used when possible for best results. 

To modify the website from the frontend, Admin or Teacher access is required. For more information on what each user role can modify, 
see [User Roles & Access Control](#user-roles--access-control). 

Major updates should include: secure user authentication, preferably through the University of Alabama system; transitioning database 
and web hosting to University of Alabama resources; additional media imbedding options for scenarios; student feedback based on scenario
results.

Tests can be run through the terminal or through Visual Studio. For more information see [Running Tests](#running-tests).

---

## Testing

### Test Project Overview

The project includes a comprehensive xUnit test suite in `PracticeBeforeThePatient.Tests`.

**Project Structure:**
```
PracticeBeforeThePatient.Tests/
├── Unit/
│   ├── DevAccessStoreTests.cs       # Role management & authentication logic
│   ├── EntityRelationshipTests.cs   # Database entity relationships & cascades
│   └── ScenarioValidationTests.cs   # Scenario model validation rules
├── Integration/
│   ├── AccessControllerTests.cs     # /api/access endpoint tests
│   ├── ScenariosControllerTests.cs  # /api/scenarios endpoint tests
│   ├── ClassManagementTests.cs      # Class/enrollment operations
│   ├── GradingTests.cs              # Submission & grading workflow
│   └── ApiTestFixture.cs            # WebApplicationFactory setup
└── Fixtures/
    ├── TestDbContextFactory.cs      # In-memory database factory
    └── TestDataSeeder.cs            # Reusable test data creation
```

### Running Tests

**Run all tests:**
```bash
dotnet test
```

**Run tests with detailed output:**
```bash
dotnet test --verbosity normal
```

**Run specific test class:**
```bash
dotnet test --filter "FullyQualifiedName~AccessControllerTests"
```

**Run tests in Visual Studio:**
- Open Test Explorer (Test → Test Explorer)
- Click **Run All** or right-click specific tests

### Unit Tests

#### DevAccessStoreTests
Tests the `DevAccessStore` service which manages user authentication state in dev mode.

| Test | Description |
|------|-------------|
| `GetCurrentEmailAsync_ReturnsSetEmail` | Verifies email is stored and retrieved correctly |
| `GetCurrentRoleAsync_ReturnsCorrectRole` | Validates role lookup from database |
| `IsTeacherAsync_ReturnsTrue_ForTeacher` | Teachers have teacher privileges |
| `IsTeacherAsync_ReturnsTrue_ForAdmin` | Admins also have teacher privileges |
| `IsTeacherAsync_ReturnsFalse_ForStudent` | Students don't have teacher privileges |
| `IsAdminAsync_ReturnsTrue_OnlyForAdmin` | Only admins have admin flag |
| `NormalizeRole_ReturnsExpectedRole` | Role normalization (instructor → teacher, etc.) |
| `SetThemeForCurrentEmailAsync_SetsTheme` | Theme preference is stored |
| `GetThemeForCurrentEmailAsync_ReturnsLightByDefault` | Default theme is light |

#### EntityRelationshipTests
Tests database entity relationships and cascade delete behavior.

| Test | Description |
|------|-------------|
| `DeletingClass_CascadesDeleteToRelatedEntities` | Deleting a class removes teachers, students, assignments, submissions |
| `UserEntity_UniqueEmailConstraint_PreventsDuplicates` | Email uniqueness is enforced |
| `ClassEntity_UniqueNameConstraint_Exists` | Class name uniqueness is enforced |
| `StudentEnrollment_CorrectlyLinksStudentToClass` | ClassStudents table links correctly |
| `Assignment_CorrectlyLinksToClassAndScenario` | Assignment foreign keys are correct |
| `Submission_CorrectlyLinksToAssignmentAndStudent` | Submission relationships are valid |

#### ScenarioValidationTests
Tests scenario model validation rules.

| Test | Description |
|------|-------------|
| `Scenario_MustHaveTitle` | Empty title is invalid |
| `Scenario_WithTitle_IsValid` | Non-empty title passes validation |
| `Scenario_HasRequiredProperties` | Model initializes with required defaults |

### Integration Tests

Integration tests use `WebApplicationFactory` to test the full HTTP pipeline.

#### AccessControllerTests
Tests `/api/access` endpoints.

| Test | Description |
|------|-------------|
| `GetAccess_ReturnsOk` | Endpoint returns 200 OK |
| `GetAccess_ReturnsAccessResponse` | Response contains required fields |
| `SetDevUser_ChangesCurrentUser` | Dev user switching works |
| `SetDevUser_ReturnsBadRequest_WhenBodyIsNull` | Null body is rejected |
| `SetTheme_UpdatesUserTheme` | Theme preference is saved |
| `AdminUser_HasTeacherAndAdminFlags` | Admin has both flags true |
| `TeacherUser_HasTeacherFlagOnly` | Teacher has only teacher flag |
| `StudentUser_HasNoElevatedFlags` | Student has no elevated access |
| `Student_SeesOnlyAssignedScenarios` | Students see only their class assignments |
| `Teacher_SeesAllScenarios` | Teachers see all scenarios |

#### ScenariosControllerTests
Tests `/api/scenarios` endpoints.

| Test | Description |
|------|-------------|
| `GetScenarios_ReturnsOk` | List endpoint returns 200 |
| `GetScenarios_ReturnsListOfScenarioIds` | Returns scenario ID list |
| `GetScenarioById_ReturnsScenario_WhenExists` | Get by ID works for existing scenarios |
| `GetScenarioById_ReturnsNotFound_WhenNotExists` | 404 for missing scenarios |

#### ClassManagementTests
Tests class and enrollment operations.

| Test | Description |
|------|-------------|
| `CreateClass_WithUniqueName_Succeeds` | Class creation works |
| `AddStudentToClass_CreatesEnrollment` | Student enrollment is created |
| `AddTeacherToClass_CreatesTeachingAssignment` | Teacher assignment is created |
| `CreateAssignment_WithScenarioAndDueDate_Succeeds` | Assignment creation works |
| `RemoveStudentFromClass_DeletesEnrollment` | Student removal works |
| `AddingStudent_CreatesUserIfNotExists` | New users are auto-created |

#### GradingTests
Tests submission and grading workflow.

| Test | Description |
|------|-------------|
| `GradeSubmission_SetsGradeAndFeedback` | Grading updates submission |
| `Grade_AcceptsValidRange` | Grades 0-100 are accepted |
| `SubmissionStates_AreDistinguishable` | Not submitted / Submitted / Graded states work |

### Test Fixtures

#### TestDbContextFactory
Creates isolated in-memory database contexts for unit tests.

```csharp
using var db = TestDbContextFactory.CreateInMemoryContext();
// Use db for isolated test operations
```

#### TestDataSeeder
Provides reusable methods for creating test data.

```csharp
var admin = TestDataSeeder.CreateAdmin(db, "admin@test.edu");
var teacher = TestDataSeeder.CreateTeacher(db, "teacher@test.edu");
var student = TestDataSeeder.CreateStudent(db, "student@test.edu");
var classEntity = TestDataSeeder.CreateClass(db, admin, "Test Class");
TestDataSeeder.AddStudentToClass(db, classEntity, student, admin);
var scenario = TestDataSeeder.CreateScenario(db, "scenario-id", "Scenario Title");
var assignment = TestDataSeeder.CreateAssignment(db, classEntity, scenario, admin);
var submission = TestDataSeeder.CreateSubmission(db, assignment, student);
```

#### ApiTestFixture
WebApplicationFactory fixture for integration tests.

```csharp
public class MyTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;
    
    public MyTests(ApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }
}
```

### Writing New Tests

1. **Add unit test:**
   ```csharp
   // In PracticeBeforeThePatient.Tests/Unit/
   public class MyServiceTests
   {
       [Fact]
       public void MyMethod_DoesExpectedThing()
       {
           // Arrange
           using var db = TestDbContextFactory.CreateInMemoryContext();
           
           // Act
           var result = /* ... */;
           
           // Assert
           Assert.Equal(expected, result);
       }
   }
   ```

2. **Add integration test:**
   ```csharp
   // In PracticeBeforeThePatient.Tests/Integration/
   public class MyControllerTests : IClassFixture<ApiTestFixture>
   {
       private readonly HttpClient _client;
       
       public MyControllerTests(ApiTestFixture fixture)
       {
           _client = fixture.CreateClient();
       }
       
       [Fact]
       public async Task Endpoint_ReturnsExpectedResult()
       {
           var response = await _client.GetAsync("/api/my-endpoint");
           Assert.Equal(HttpStatusCode.OK, response.StatusCode);
       }
   }
   ```

### Test Coverage Summary

| Area | Unit Tests | Integration Tests |
|------|------------|-------------------|
| Access Control | ✅ DevAccessStore | ✅ AccessController |
| Scenarios | ✅ Validation | ✅ ScenariosController |
| Classes | ✅ Relationships | ✅ ClassManagement |
| Grading | ✅ Entity States | ✅ GradingTests |
| Users | ✅ Roles | ✅ Permissions |

### Future Testing Improvements

1. **UI Tests** — Add Playwright or bUnit for Blazor component testing
2. **Performance Tests** — Load testing for concurrent users
3. **Code Coverage** — Add coverage reporting with Coverlet

---

## Backlog & Known Issues

### GitHub Issues

Track issues at: https://github.com/kieraschnell/CS495-PracticeBeforeThePatient/issues

### Known Issues

| Issue | Severity | Description | Workaround |
|-------|----------|-------------|------------|
| Migration bootstrap on existing DB | Medium | Switching from `EnsureCreated` to `Migrate` fails on existing databases | Delete `app.db` or manually insert migration history |
| Dev user state is in-memory | Low | Switching users doesn't persist across API restarts | User must re-select after restart |
| No password authentication | High | Dev mode only; needs SSO integration for production | Use for development/demo only |
| Scenario validation limited | Low | Complex branching validation not implemented | Manual review of scenarios |

### Major Future Work

1. **SSO Integration** — Replace dev user switching with real authentication (Azure AD, OAuth)
2. **Real-time Collaboration** — SignalR for live scenario editing
3. **Analytics Dashboard** — Track student progress and common mistakes
4. **Scenario Import/Export** — JSON file upload/download
5. **Mobile Optimization** — Responsive design improvements

---

## FAQs

### End User FAQs

**Q: I don't see any scenarios in the Simulation dropdown. What's wrong?**

A: You need to be enrolled in a class with active assignments. Contact your instructor to ensure:
1. You're added to the class roster
2. The class has assignments with scenarios
3. Assignment due dates haven't passed

**Q: I submitted my simulation but don't see a grade. When will it appear?**

A: Grades appear after your instructor reviews and grades your submission. Check the **Grades** page periodically, or contact your instructor for the grading timeline.

**Q: How do I switch between student and teacher views for testing?**

A: Go to **Settings** page and select a different user from the "Switch User" dropdown. Each user has a predefined role:
- `admin@ua.edu` — Admin
- `instructor@ua.edu` — Teacher
- `student1@ua.edu`, `student2@ua.edu` — Students

**Q: Can I create my own scenarios?**

A: Only teachers and admins can create/edit scenarios. If you're a teacher, access the **Scenario Editor** from the sidebar.

### Developer/Admin FAQs

**Q: The API won't start after pulling latest changes. How do I fix it?**

A: Most likely a database schema mismatch. Try:
1. Check for pending migrations: `dotnet ef migrations list --project PracticeBeforeThePatient.Api --startup-project PracticeBeforeThePatient.Api`
2. Apply migrations: `dotnet ef database update --project PracticeBeforeThePatient.Api --startup-project PracticeBeforeThePatient.Api`
3. If migration fails on existing data, delete `app.db` and restart (WARNING: loses data)

**Q: How do I add a new admin user?**

A: Three options:
1. Use Admin Management page (if you already have admin access)
2. Modify seed data in `Program.cs` and reset database
3. Direct SQL: `UPDATE Users SET Role = 'admin' WHERE Email = 'user@example.com';`

**Q: The production deployment failed. How do I debug?**

A: SSH into the VM and check:
```bash
# View API logs
sudo journalctl -u pbtp-api -n 200 --no-pager

# View Web logs
sudo journalctl -u pbtp-web -n 200 --no-pager

# Check service status
sudo systemctl status pbtp-api
sudo systemctl status pbtp-web
```

Common issues:
- Migration errors (table already exists)
- Permission errors on Data directory
- Port conflicts

**Q: How do I back up the database?**

A: SQLite database is a single file. Copy it:
```bash
# Local
cp PracticeBeforeThePatient.Api/Data/app.db backup-$(date +%Y%m%d).db

# Production
sudo cp /var/www/pbtp-api/Data/app.db /home/user/backup-$(date +%Y%m%d).db
```

---

## External Resources

| Resource | Type | Cost | Purpose |
|----------|------|------|---------|
| GCP Compute Engine | Hosting | Free tier / Pay-as-you-go | Production VM |
| GitHub | Source Control | Free | Repository, Actions CI/CD |
| Let's Encrypt | SSL | Free | HTTPS certificates via Caddy |
| SQLite | Database | Free | Embedded database |

**No paid external APIs or services are required for core functionality.**

---

## Contact & Support

- **Repository**: https://github.com/kieraschnell/CS495-PracticeBeforeThePatient
- **Issues**: https://github.com/kieraschnell/CS495-PracticeBeforeThePatient/issues

---

*Last updated: March 2026*
