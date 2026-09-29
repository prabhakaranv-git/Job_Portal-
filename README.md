# Job Portal Backend — Production-Grade Web API

A robust, production-grade backend for a Job Portal Management System built with **C#**, **.NET 8**, **ASP.NET Core Web API**, **Entity Framework Core 8**, **PostgreSQL**, and **Clean Layered Architecture**.

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Architecture & Layer Dependencies](#architecture--layer-dependencies)
3. [Technology Stack](#technology-stack)
4. [Authentication & Authorization (Phase 2)](#authentication--authorization-phase-2)
   - [Authentication Architecture](#authentication-architecture)
   - [Refresh Token Security & Rotation](#refresh-token-security--rotation)
   - [Role-Based Access Control & Recruiter Approval](#role-based-access-control--recruiter-approval)
   - [Rate Limiting](#rate-limiting)
5. [Company Management & Job Lifecycle (Phase 3)](#company-management--job-lifecycle-phase-3)
   - [Company Ownership Architecture](#company-ownership-architecture)
   - [Job Lifecycle State Machine](#job-lifecycle-state-machine)
   - [Public Job Search & Filtering](#public-job-search--filtering)
   - [Skill Taxonomy & Association](#skill-taxonomy--association)
6. [Resume Management & Job Applications (Phase 4)](#resume-management--job-applications-phase-4)
   - [Resume Management & Storage Architecture](#resume-management--storage-architecture)
   - [Application Lifecycle & State Machine](#application-lifecycle--state-machine)
   - [Recruiter Application Pipeline](#recruiter-application-pipeline)
   - [Integrity & Duplicate Protection](#integrity--duplicate-protection)
7. [Recruiter Notes & Candidate Feedback (Phase 5)](#recruiter-notes--candidate-feedback-phase-5)
   - [Recruiter Notes Subsystem](#recruiter-notes-subsystem)
   - [Candidate Feedback & Evaluation](#candidate-feedback--evaluation)
   - [Strict Multi-Tenant & Role Isolation](#strict-multi-tenant--role-isolation)
8. [Prerequisites](#prerequisites)
9. [Environment Variables](#environment-variables)
10. [Local Development Setup](#local-development-setup)
11. [Database & Migrations](#database--migrations)
12. [Running with Docker Compose](#running-with-docker-compose)
13. [API Documentation & Swagger](#api-documentation--swagger)
14. [Available API Endpoints](#available-api-endpoints)
15. [Testing & Verification](#testing--verification)
16. [Phase 6 Recommendations](#phase-6-recommendations)

---

## Project Overview

The Job Portal platform manages recruitment workflows across three key user roles:
- **Job Seeker**: Discovers opportunities, manages resumes, selects default resumes, and submits/tracks applications.
- **Recruiter**: Represents companies, publishes job listings, reviews applicants, downloads resumes, maintains private candidate notes, and records structured feedback evaluations. *Requires administrative approval before accessing recruiter features.*
- **Admin**: Approves or rejects recruiter registrations, manages skill taxonomies, oversees platform operations, and moderates content.

---

## Architecture & Layer Dependencies

The system follows a strict **Clean Layered Architecture**:

```text
JobPortal.API (Presentation & Web API Host)
      │
      ▼
JobPortal.Application (Business Logic, Services, Validators & Contracts)
      │
      ▼
JobPortal.Domain (Pure Entities, Enums & Exceptions)
      ▲
      │
JobPortal.Infrastructure (EF Core, Npgsql, BCrypt, JWT, File Storage, Persistence)
```

- **Domain (`JobPortal.Domain`)**: Core entities (`User`, `Role`, `JobSeeker`, `Recruiter`, `Company`, `Job`, `Application`, `Resume`, `Skill`, `JobSkill`, `RefreshToken`, `RecruiterNote`, `CandidateFeedback`), domain enums (`JobStatus`, `JobType`, `ApplicationStatus`, `UserRole`, `RecruiterApprovalStatus`, `FeedbackRecommendation`), and domain exceptions. Zero external framework dependencies.
- **Application (`JobPortal.Application`)**: Service contracts (`IAuthService`, `IAdminService`, `ICompanyService`, `IJobService`, `ISkillService`, `IResumeService`, `IJobApplicationService`, `IApplicationStatusTransitionService`, `IRecruiterNoteService`, `ICandidateFeedbackService`, `IHealthService`), DTOs, FluentValidation validators, and core business workflow logic.
- **Infrastructure (`JobPortal.Infrastructure`)**: PostgreSQL persistence via EF Core 8, `ApplicationDbContext` with automatic UTC timestamp auditing, BCrypt hashing (work factor: 12), SHA-256 hashed refresh token persistence, JWT generation, local file storage abstraction (`LocalFileStorageService`), composite database indexes, and EF Core migrations.
- **API (`JobPortal.API`)**: HTTP routing (`/api/v1/...`), Serilog structured logging, request correlation (`X-Request-ID`), global Problem Details exception handling (RFC 7807), security response headers, fixed-window rate limiting, and health probes.
- **Tests (`JobPortal.Tests`)**: xUnit test suite featuring unit tests, validation tests, security tests, and integration tests via `WebApplicationFactory`.

---

## Technology Stack

| Category | Technology |
|---|---|
| Runtime | .NET 8 (C# 12) |
| Web Framework | ASP.NET Core Web API |
| ORM & Data Access | Entity Framework Core 8 / Npgsql.EntityFrameworkCore.PostgreSQL |
| Database | PostgreSQL 16 (Neon Serverless PostgreSQL) |
| File Storage | Streaming Local File Storage Abstraction (`IFileStorageService`) |
| Validation | FluentValidation |
| Logging | Serilog (Structured Console Logging & Trace Correlation) |
| Password Hashing | BCrypt.Net-Next (Work Factor: 12) |
| Token Management | ASP.NET Core JWT Bearer & System.IdentityModel.Tokens.Jwt |
| Rate Limiting | ASP.NET Core RateLimiting (Fixed Window) |
| Documentation | Swagger / OpenAPI (Swashbuckle) |
| Containerization | Docker (Multi-stage build) & Docker Compose |
| Testing | xUnit, FluentAssertions, Moq, Microsoft.AspNetCore.Mvc.Testing |

---

## Authentication & Authorization (Phase 2)

### Authentication Architecture

Authentication uses short-lived **JWT Access Tokens** paired with long-lived, revocable **Refresh Tokens**:

- **Access Token Lifespan**: 15 minutes (configurable via `Jwt:AccessTokenExpirationMinutes`).
- **Refresh Token Lifespan**: 7 days (configurable via `Jwt:RefreshTokenExpirationDays`).
- **Token Claims**: `sub` (UserId), `email`, `role`, `firstName`, `lastName`, `jti`, `iat`, `nbf`, `exp`, `iss`, `aud`.

### Refresh Token Security & Rotation

1. **Cryptographic Generation**: Refresh tokens are 64 cryptographically random bytes generated via `RandomNumberGenerator`.
2. **Hashed Persistence**: Only SHA-256 hashes of refresh tokens are stored in the database (`RefreshTokens.Token`). Plaintext tokens are never stored at rest.
3. **Single-Use Sliding Rotation**: Every successful refresh call revokes the old token (`RevokedAt = UtcNow`, `ReplacedByToken = <new_token_hash>`) and issues a fresh token pair.
4. **Token Reuse Detection**: If an already-revoked refresh token is presented, the system assumes token compromise, automatically invalidates all active sessions for that user, logs a security alert, and returns `401 Unauthorized`.
5. **Session Revocation on Password Change**: Changing a password immediately revokes all active refresh tokens.

---

## Company Management & Job Lifecycle (Phase 3)

### Company Ownership Architecture

- **Recruiter Association**: An approved recruiter can create a company profile (`POST /api/v1/companies`). The recruiter is automatically bound to the newly created company (`Recruiter.CompanyId`).
- **Single-Company Constraint**: A recruiter can only belong to one company. Attempting to create a second company returns `409 Conflict`.
- **Ownership Verification**: Company updates (`PUT /api/v1/companies/{id}`) require recruiter approval and company ownership (`recruiter.CompanyId == id`). Cross-recruiter modifications return `403 Forbidden`.
- **Administrative Verification**: The `IsVerified` flag is administrative and cannot be altered through recruiter create/update requests.

### Job Lifecycle State Machine

```text
                 ┌──────────────┐
                 │    DRAFT     │
                 └──────┬───────┘
                        │ publish
                        ▼
                 ┌──────────────┐
                 │  PUBLISHED   │
                 └──────┬───────┘
                   │           │
                close        expire (runtime query)
                   │           │
                   ▼           ▼
              ┌────────┐   ┌────────┐
              │ CLOSED │   │ CLOSED │
              └────┬───┘   └────┬───┘
                   │             │
                   └──────┬──────┘
                          ▼
                     ┌─────────┐
                     │ ARCHIVED│
                     └─────────┘
```

---

## Resume Management & Job Applications (Phase 4)

### Resume Management & Storage Architecture

- **Supported Formats**: `.pdf`, `.doc`, `.docx`.
- **Maximum File Size**: 5 MB (configurable via `FileStorage:ResumeMaxSizeBytes`).
- **Server-Side File Security**: Original filenames are treated solely as metadata. Physical storage uses non-guessable GUID filenames (`{guid}.pdf`) stored under isolated user directories (`uploads/resumes/{userId}/{storedFileName}`).
- **Path Traversal Protection**: All paths are sanitized and validated against the base storage root using `Path.GetFullPath`.
- **Default Resume Rules**:
  - The first uploaded resume automatically becomes the default resume.
  - A Job Seeker can have at most one default resume. Setting a new default unsets the previous default in an atomic transaction.
  - Deleting a default resume automatically promotes the newest remaining resume to default.

### Application Lifecycle & State Machine

Job applications progress through a strict, deterministic state machine:

```text
Submitted
   ├──> UnderReview
   ├──> Rejected
   └──> Withdrawn

UnderReview
   ├──> Shortlisted
   ├──> Rejected
   └──> Withdrawn

Shortlisted
   ├──> Interviewing
   ├──> Rejected
   └──> Withdrawn

Interviewing
   ├──> Accepted
   ├──> Rejected
   └──> Withdrawn
```

- **Terminal States**: `Accepted`, `Rejected`, and `Withdrawn` are terminal; no further status transitions are permitted once reached.
- **Candidate Withdrawal**: Job seekers can withdraw active applications at any stage prior to reaching terminal states (`PATCH /api/v1/jobseeker/applications/{id}/withdraw`).
- **Invalid Transitions**: Any invalid transition immediately returns `409 Conflict` with a ProblemDetails response.

### Recruiter Application Pipeline

- **Application Review**: Approved recruiters can inspect applicant details, cover letters, and application stages for jobs they own (`GET /api/v1/recruiter/jobs/{jobId}/applications`).
- **Resume Streaming**: Recruiters can download applicant resumes securely via `GET /api/v1/recruiter/applications/{id}/resume` without exposing server filesystem paths.
- **Cross-Recruiter Protection**: Recruiter A is strictly prevented from inspecting, downloading resumes from, or updating status for applications submitted to Recruiter B's jobs (`403 Forbidden`).

### Integrity & Duplicate Protection

- **Database Unique Constraint**: `(JobId, JobSeekerId)` unique constraint in PostgreSQL prevents race conditions and duplicate applications.
- **Application Validation**: Checks job existence, published status, unexpired expiration date, resume ownership, and cover letter character limits (max 5000 characters).

---

## Recruiter Notes & Candidate Feedback (Phase 5)

### Recruiter Notes Subsystem

Approved recruiters can maintain private, internal notes against candidate applications for jobs they own:
- **Note Content**: Required, 1 to 5000 characters (whitespace trimmed, whitespace-only rejected).
- **Pagination & Sorting**: Notes are listed with pagination (`page`, `pageSize` default 20) with deterministic database sorting (`CreatedAt DESC, Id DESC`).
- **Operations**:
  - `POST /api/v1/recruiter/applications/{applicationId}/notes` (Create note)
  - `GET /api/v1/recruiter/applications/{applicationId}/notes` (List notes with pagination)
  - `GET /api/v1/recruiter/applications/{applicationId}/notes/{noteId}` (Get note by ID)
  - `PUT /api/v1/recruiter/applications/{applicationId}/notes/{noteId}` (Update note content)
  - `DELETE /api/v1/recruiter/applications/{applicationId}/notes/{noteId}` (Delete note)

### Candidate Feedback & Evaluation

Structured evaluations provide standardized candidate scoring without automated hiring side-effects:
- **Overall Rating**: Integer `1` to `5`:
  - `1 = Very Poor`, `2 = Below Expectations`, `3 = Meets Expectations`, `4 = Strong`, `5 = Exceptional`
- **Recommendation Enum**: `StrongHire`, `Hire`, `Maybe`, `NoHire`, `StrongNoHire`.
- **Text Sections**:
  - `Strengths`: Max 3000 characters (optional)
  - `Weaknesses`: Max 3000 characters (optional)
  - `DetailedFeedback`: Max 5000 characters (optional)
  - *Validation Rule*: At least one meaningful text field must be provided beyond the rating.
- **Cardinality & Uniqueness**: Exactly one active feedback record per recruiter per application, enforced at the database level by `UNIQUE(ApplicationId, RecruiterId)`. Attempted duplicate submissions safely return `409 Conflict`.
- **Advisory Principle**: Candidate feedback is internal and advisory. Creating feedback or selecting `StrongHire` never automatically transitions application status.

### Strict Multi-Tenant & Role Isolation

- **Recruiter Isolation**: A recruiter may only create, view, update, or delete notes/feedback for applications belonging to jobs that recruiter owns (`recruiter.Id == application.Job.RecruiterId`). Attempted cross-recruiter access returns `403 Forbidden`.
- **Job Seeker Protection**: Job Seekers are strictly denied access to all recruiter notes and feedback endpoints (`403 Forbidden`). Internal notes are never exposed in public or applicant-facing queries.
- **Approval Policy**: Only recruiters approved by administrators (`RecruiterApprovalStatus.Approved`) can access Phase 5 endpoints. Unapproved recruiters receive `403 Forbidden`.

---

## Environment Variables

| Variable | Description | Default / Example |
|---|---|---|
| `POSTGRES_DB` | PostgreSQL database name | `jobportal_db` |
| `POSTGRES_USER` | PostgreSQL superuser | `postgres` |
| `POSTGRES_PASSWORD` | PostgreSQL password | `postgres` |
| `POSTGRES_PORT` | PostgreSQL host port | `5432` |
| `ConnectionStrings__DefaultConnection` | Database connection string | `Host=localhost;Port=5432;Database=jobportal_db;Username=postgres;Password=postgres` |
| `Jwt__Issuer` | JWT Token Issuer | `JobPortalAPI` |
| `Jwt__Audience` | JWT Token Audience | `JobPortalClients` |
| `Jwt__SecretKey` | HMAC-SHA256 Signing Secret (min 32 chars) | `CHANGE_ME_IN_PRODUCTION_SUPER_SECRET_KEY_MINIMUM_32_BYTES_LONG!` |
| `Jwt__AccessTokenExpirationMinutes` | Access token lifespan (minutes) | `15` |
| `Jwt__RefreshTokenExpirationDays` | Refresh token lifespan (days) | `7` |
| `FileStorage__ResumeStoragePath` | Relative storage path for resumes | `uploads/resumes` |
| `FileStorage__ResumeMaxSizeBytes` | Maximum allowed resume file size | `5242880` (5MB) |
| `Application__Name` | API Application Title | `JobPortal API` |
| `Application__Version` | API Semantic Version | `1.0.0` |
| `ASPNETCORE_ENVIRONMENT` | Hosting environment | `Development` / `Production` |
| `ASPNETCORE_URLS` | Binding URLs | `http://+:5000` |

---

## Local Development Setup

### 1. Clone & Restore

```bash
git clone <repository-url>
cd Job-P
dotnet restore
```

### 2. Build the Solution

```bash
dotnet build
```

---

## Database & Infrastructure (Neon PostgreSQL)

The Job Portal Backend is powered by **PostgreSQL hosted on Neon** with SSL encryption and connection pooling:

- **Database Provider**: Managed Serverless PostgreSQL (Neon)
- **Connection Pooling**: Supported via Neon's connection pooler (`-pooler` endpoint) and Npgsql connection pooling
- **Security & SSL**: SSL Mode is required (`SSL Mode=Require;Trust Server Certificate=true;`)
- **Environment Driven**: Zero hardcoded credentials in source code. Fully configured via `ConnectionStrings__DefaultConnection` or `DATABASE_URL`

### Apply Migrations to Neon PostgreSQL

Ensure your `.env` contains your Neon connection string, then execute:

```bash
dotnet ef database update --project JobPortal.Infrastructure --startup-project JobPortal.Infrastructure
```

### Applied Migrations:
1. `20260929040654_InitialCreate` — Core domain tables, constraints, and initial role seed data.
2. `20260929042502_Phase2Authentication` — Recruiter approval status enum, audit timestamps, and rejection reasons.
3. `20260929050925_Phase3CompanyJobManagement` — Job search performance indexes (`Location`, `JobType`, `ExpiresAt`, `CreatedAt`) and composite indexes (`Status, CreatedAt`, `Status, ExpiresAt`, `RecruiterId, Status`).
4. `20260929053654_Phase4ResumeAndApplications` — StoredFileName property, resume default partial index, application pipeline indexes, and restrict delete constraints.
5. `20260929093559_Phase5RecruiterNotesAndCandidateFeedback` — RecruiterNotes and CandidateFeedbacks tables, indexes, unique constraints, and restrictive foreign keys.

---

## Running with Docker Compose

To launch PostgreSQL 16 and the ASP.NET Core API container together:

```bash
docker compose up --build
```

To stop containers:

```bash
docker compose down
```

---

## API Documentation & Swagger

When running locally in `Development` mode:
- **Swagger UI**: [http://localhost:5000](http://localhost:5000) (or `http://localhost:5000/swagger`)
- **OpenAPI Spec**: [http://localhost:5000/swagger/v1/swagger.json](http://localhost:5000/swagger/v1/swagger.json)

---

## Available API Endpoints

### Recruiter Notes (`/api/v1/recruiter/applications/{applicationId}/notes`)

| Method | Endpoint | Access | Description | Status Codes |
|---|---|---|---|---|
| `POST` | `/api/v1/recruiter/applications/{applicationId}/notes` | Approved Recruiter (Owner) | Creates a private recruiter note | `201 Created`, `400`, `401`, `403`, `404` |
| `GET` | `/api/v1/recruiter/applications/{applicationId}/notes` | Approved Recruiter (Owner) | Lists notes for application (paginated) | `200 OK`, `400`, `401`, `403`, `404` |
| `GET` | `/api/v1/recruiter/applications/{applicationId}/notes/{noteId}` | Approved Recruiter (Owner) | Gets specific note details | `200 OK`, `401`, `403`, `404` |
| `PUT` | `/api/v1/recruiter/applications/{applicationId}/notes/{noteId}` | Approved Recruiter (Owner) | Updates existing note content | `200 OK`, `400`, `401`, `403`, `404` |
| `DELETE` | `/api/v1/recruiter/applications/{applicationId}/notes/{noteId}` | Approved Recruiter (Owner) | Deletes a recruiter note | `200 OK`, `401`, `403`, `404` |

### Candidate Feedback (`/api/v1/recruiter/applications/{applicationId}/feedback`)

| Method | Endpoint | Access | Description | Status Codes |
|---|---|---|---|---|
| `POST` | `/api/v1/recruiter/applications/{applicationId}/feedback` | Approved Recruiter (Owner) | Records structured evaluation feedback | `201 Created`, `400`, `401`, `403`, `404`, `409` |
| `GET` | `/api/v1/recruiter/applications/{applicationId}/feedback` | Approved Recruiter (Owner) | Gets evaluation feedback for application | `200 OK`, `401`, `403`, `404` |
| `PUT` | `/api/v1/recruiter/applications/{applicationId}/feedback` | Approved Recruiter (Owner) | Updates existing evaluation feedback | `200 OK`, `400`, `401`, `403`, `404` |
| `DELETE` | `/api/v1/recruiter/applications/{applicationId}/feedback` | Approved Recruiter (Owner) | Deletes evaluation feedback | `200 OK`, `401`, `403`, `404` |

### Resumes (`/api/v1/jobseeker/resumes`)

| Method | Endpoint | Access | Description | Status Codes |
|---|---|---|---|---|
| `POST` | `/api/v1/jobseeker/resumes` | JobSeeker | Uploads resume (multipart/form-data) | `201 Created`, `400`, `401`, `403` |
| `GET` | `/api/v1/jobseeker/resumes` | JobSeeker | Lists all resumes for current user | `200 OK`, `401`, `403` |
| `GET` | `/api/v1/jobseeker/resumes/{id}` | JobSeeker (Owner) | Gets resume metadata by ID | `200 OK`, `401`, `403`, `404` |
| `GET` | `/api/v1/jobseeker/resumes/{id}/download` | JobSeeker (Owner) | Downloads physical resume document | `200 OK`, `401`, `403`, `404` |
| `PATCH` | `/api/v1/jobseeker/resumes/{id}/default` | JobSeeker (Owner) | Sets specified resume as default | `200 OK`, `401`, `403`, `404` |
| `DELETE` | `/api/v1/jobseeker/resumes/{id}` | JobSeeker (Owner) | Deletes resume and storage file | `200 OK`, `401`, `403`, `404` |

### Applications (`/api/v1/jobs` & `/api/v1/jobseeker/applications`)

| Method | Endpoint | Access | Description | Status Codes |
|---|---|---|---|---|
| `POST` | `/api/v1/jobs/{jobId}/apply` | JobSeeker | Submits application for a published job | `200 OK`, `400`, `401`, `403`, `404`, `409` |
| `GET` | `/api/v1/jobseeker/applications` | JobSeeker | Lists application history with pagination & status filter | `200 OK`, `400`, `401`, `403` |
| `GET` | `/api/v1/jobseeker/applications/{id}` | JobSeeker (Owner) | Gets application details by ID | `200 OK`, `401`, `403`, `404` |
| `PATCH` | `/api/v1/jobseeker/applications/{id}/withdraw` | JobSeeker (Owner) | Withdraws active job application | `200 OK`, `401`, `403`, `404`, `409` |

### Recruiter Pipeline (`/api/v1/recruiter`)

| Method | Endpoint | Access | Description | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/v1/recruiter/jobs/{jobId}/applications` | Approved Recruiter (Owner) | Lists applications for recruiter's job | `200 OK`, `400`, `401`, `403`, `404` |
| `GET` | `/api/v1/recruiter/applications/{id}` | Approved Recruiter (Owner) | Gets applicant details and cover letter | `200 OK`, `401`, `403`, `404` |
| `GET` | `/api/v1/recruiter/applications/{id}/resume` | Approved Recruiter (Owner) | Downloads applicant resume document | `200 OK`, `401`, `403`, `404` |
| `PATCH` | `/api/v1/recruiter/applications/{id}/status` | Approved Recruiter (Owner) | Updates application pipeline stage | `200 OK`, `400`, `401`, `403`, `404`, `409` |

### Companies (`/api/v1/companies`)

| Method | Endpoint | Access | Description | Status Codes |
|---|---|---|---|---|
| `POST` | `/api/v1/companies` | Approved Recruiter | Creates company and associates recruiter | `201 Created`, `400`, `401`, `403`, `409` |
| `GET` | `/api/v1/companies/{id}` | Anonymous / Public | Gets company details by ID | `200 OK`, `404 Not Found` |
| `PUT` | `/api/v1/companies/{id}` | Approved Recruiter (Owner) | Updates company details | `200 OK`, `400`, `401`, `403`, `404` |

### Jobs (`/api/v1/jobs`)

| Method | Endpoint | Access | Description | Status Codes |
|---|---|---|---|---|
| `POST` | `/api/v1/jobs` | Approved Recruiter | Creates a job posting in Draft status | `201 Created`, `400`, `401`, `403`, `409` |
| `GET` | `/api/v1/jobs` | Anonymous / Public | Searches active, published jobs with filters & pagination | `200 OK`, `400 Bad Request` |
| `GET` | `/api/v1/jobs/{id}` | Anonymous / Public | Gets details of an active published job | `200 OK`, `404 Not Found` |
| `PUT` | `/api/v1/jobs/{id}` | Approved Recruiter (Owner) | Updates job posting details and skills | `200 OK`, `400`, `401`, `403`, `404` |
| `PUT` | `/api/v1/jobs/{id}/publish` | Approved Recruiter (Owner) | Publishes draft job (Draft -> Published) | `200 OK`, `401`, `403`, `404`, `409` |
| `PUT` | `/api/v1/jobs/{id}/close` | Approved Recruiter (Owner) | Closes published job (Published -> Closed) | `200 OK`, `401`, `403`, `404`, `409` |
| `PUT` | `/api/v1/jobs/{id}/archive` | Approved Recruiter (Owner) | Archives job (Draft/Pub/Closed -> Archived) | `200 OK`, `401`, `403`, `404`, `409` |

### Skills Taxonomy (`/api/v1/skills`)

| Method | Endpoint | Access | Description | Status Codes |
|---|---|---|---|---|
| `POST` | `/api/v1/skills` | Admin | Creates a new skill in the taxonomy | `201 Created`, `400`, `401`, `403`, `409` |
| `GET` | `/api/v1/skills` | Anonymous / Public | Lists all available skills in taxonomy | `200 OK` |

### Authentication (`/api/v1/auth`)

| Method | Endpoint | Access | Description | Status Codes |
|---|---|---|---|---|
| `POST` | `/api/v1/auth/register` | Anonymous | Registers JobSeeker or Recruiter | `200 OK`, `400 Bad Request`, `409 Conflict` |
| `POST` | `/api/v1/auth/login` | Anonymous | Authenticates user & issues token pair | `200 OK`, `400 Bad Request`, `401 Unauthorized` |
| `POST` | `/api/v1/auth/refresh-token` | Anonymous | Rotates refresh token & issues new pair | `200 OK`, `400 Bad Request`, `401 Unauthorized` |
| `POST` | `/api/v1/auth/revoke-token` | Anonymous / Auth | Revokes refresh token (Logout) | `200 OK`, `400 Bad Request`, `403 Forbidden` |
| `POST` | `/api/v1/auth/change-password` | Authenticated | Changes password & revokes all sessions | `200 OK`, `400 Bad Request`, `401 Unauthorized` |
| `GET` | `/api/v1/auth/me` | Authenticated | Returns current user profile details | `200 OK`, `401 Unauthorized` |

### Administration (`/api/v1/admin`)

| Method | Endpoint | Access | Description | Status Codes |
|---|---|---|---|---|
| `PUT` | `/api/v1/admin/recruiters/{id}/approve` | Admin | Approves a recruiter account | `200 OK`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found` |
| `PUT` | `/api/v1/admin/recruiters/{id}/reject` | Admin | Rejects a recruiter application | `200 OK`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found` |
| `GET` | `/api/v1/admin/recruiters/pending` | Admin | Lists all pending recruiters | `200 OK`, `401 Unauthorized`, `403 Forbidden` |

### Health & Observability

| Method | Endpoint | Access | Description | Status Codes |
|---|---|---|---|---|
| `GET` | `/api/v1/health` | Anonymous | Comprehensive health probe | `200 OK`, `503 Service Unavailable` |
| `GET` | `/health` | Anonymous | ASP.NET Core health check | `200 OK`, `503 Service Unavailable` |
| `GET` | `/health/live` | Anonymous | Container liveness probe | `200 OK` |
| `GET` | `/health/ready` | Anonymous | Database readiness probe | `200 OK`, `503 Service Unavailable` |

---

## Testing & Verification

Execute all automated unit and integration tests with:

```bash
dotnet test
```

### Test Results:
```text
Passed!  - Failed: 0, Passed: 194, Skipped: 0, Total: 194, Duration: 37 s - JobPortal.Tests.dll (net8.0)
```

- **Recruiter Notes**: Creation, paginated retrieval (`page`, `pageSize`, `CreatedAt DESC`), individual retrieval, note update, note deletion, content length/whitespace validation, cross-recruiter multi-tenant isolation, JobSeeker and anonymous access blocking.
- **Candidate Feedback**: Rating (1-5) and recommendation enum validation, text limits (strengths/weaknesses <= 3000, detailed feedback <= 5000), single feedback per recruiter per application enforced by DB unique index and conflict handling (`409 Conflict`), retrieval, update, deletion, cross-recruiter isolation, JobSeeker and unapproved recruiter blocking.
- **Resume Management**: Multi-format upload (PDF/DOCX), default resume auto-selection, default switching, file deletion with newest resume promotion, extension/MIME/size validation, ownership security isolation.
- **Application Workflow**: JobSeeker application submission, duplicate prevention (DB unique constraint + application validation), draft/closed/expired job application rejection, cover letter character limit validation, application withdrawal.
- **Recruiter Pipeline**: Application query with pagination & status filtering, applicant profile review, applicant resume streaming download, status state machine transitions (`Submitted` -> `UnderReview` -> `Shortlisted` -> `Interviewing` -> `Accepted`), invalid transition rejection, cross-recruiter ownership protection.
- **Regression Suite**: 100% pass rate across all Phase 1 (Foundation), Phase 2 (Authentication), Phase 3 (Company & Jobs), and Phase 4 (Resume & Applications) tests.

---

## Phase 6 Recommendations

1. **Email & In-App Notifications**: Asynchronous event notifications for application submission, stage advancement, and feedback submission.
2. **Interview Scheduling Subsystem**: Calendar slot scheduling, multi-interviewer assignment, and candidate interview details.
3. **Admin Content Moderation**: Administrative moderation dashboard for recruiter profiles, company verification, and job posting audits.
4. **Cloud Storage Provider**: S3 / Azure Blob Storage implementation of `IFileStorageService` for scalable cloud object persistence.
