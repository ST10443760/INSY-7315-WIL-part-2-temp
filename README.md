# Thrive Wellness Pilates

A booking and studio-management system for Thrive Wellness Pilates, built with ASP.NET Core MVC (.NET 8) and PostgreSQL.

Live site: https://thrive-wellness-pilates.onrender.com

The site runs on a free hosting tier. The first request after a period of inactivity can take 30 to 60 seconds while the server starts back up.

## Team

Null Devs

| Member | Responsibilities as planned in Task 1 |
|---|---|
| Muaaz Gaffoor | Project lead, requirements, scaffold, session management, booking system, unit tests |
| Monwabisi Dlokweni | Authentication, payment tracking, database and security design |
| Jean Rust | Email service, waitlist, client-facing pages, CI/CD and hosting |
| Lihle Joyisa | Admin dashboard, responsive design, architecture and UML |

## Features

### Client

- New versus returning client detection by email, so a returning client does not have to re-type their details.
- An intake form with consent, shown only to new clients.
- EFT and cash payment options, confirmed by an admin (see Known limitations).
- A waitlist with automatic promotion when a spot opens up.
- Cancellation through a single-use link sent by email, no login needed.

### Admin

- A dashboard with booking counts, upcoming sessions, pending payments and recent bookings.
- Session management: create, edit, delete, and mark a session full or open.
- Payment confirmation for EFT and cash payments.
- Location management.
- A client overview with search.
- Waitlist management, including manually notifying or removing an entry.

### Automated emails

- A confirmation email when a booking is created.
- A welcome email when an admin confirms a payment.
- A scheduled reminder email, sent the day before a session.
- A scheduled location email, sent on the day of a session to new clients.
- A cancellation link that frees the slot and promotes the next person on the waitlist.

## Tech stack

| Area | Technology |
|---|---|
| Framework | ASP.NET Core MVC, .NET 8 |
| Data access | Entity Framework Core 8, Npgsql |
| Database | PostgreSQL, hosted on Railway |
| Email | SendGrid |
| Containers | Docker |
| Hosting | Render |
| CI/CD | GitHub Actions |

## Architecture

The app follows MVC: controllers handle HTTP requests and validation only,
views are server-rendered Razor pages, and models and view models carry data
between the two. Business rules live in a service layer, not in controllers,
so the same rule cannot drift between two places that both need it, and it
can be unit tested without starting the web app. Data access goes through
the Repository pattern: every table has a thin repository behind an
interface, and services depend on the interface, never on the database
context directly. Payment confirmation uses the Observer pattern: the
payment service raises an event when a payment is confirmed, and the
notification service reacts to it without the payment service knowing an
email will be sent.

Project structure (`ThriveWellness/`):

```
ThriveWellness/
  Controllers/              MVC controllers
  Data/                     ApplicationDbContext
  Migrations/                EF Core migrations
  Models/                    Entities and view models
  Repositories/
    Interfaces/
    Implementations/
  Services/
    Interfaces/
    Implementations/
  Views/                     Razor views, one folder per controller
  wwwroot/                   Static files (css, js, images)
  Dockerfile
  Program.cs                 Composition root and HTTP pipeline
```

A new booking request flows like this: the client submits the schedule
page's booking form, `BookingController` validates it and calls
`BookingService`, which checks capacity, creates the client (if new), the
booking, the payment, and the intake form through their repositories, then
calls `NotificationService` to send the confirmation email through
`EmailSender`. Control returns to the controller, which redirects to a
confirmation page.

See [docs/CODE_WALKTHROUGH.md](docs/CODE_WALKTHROUGH.md) for a full,
file-by-file trace of this and every other feature, plus the design
decisions and bugs found along the way.

## Run locally

### Prerequisites

- .NET 8 SDK
- A PostgreSQL database (a free instance on a host such as Railway works, or a local install)
- The `dotnet-ef` tool, for running migrations: `dotnet tool install --global dotnet-ef`

### Clone

```
git clone <repository-url>
cd INSY-7315-WIL-part-2-temp
```

### Configuration

Settings are read from `ThriveWellness/appsettings.Development.json` when
running locally (this file is not committed; copy the keys below into it).
Every setting can also be set as an environment variable instead; nested
settings use a double underscore in place of the colon, for example
`Admin__Password`.

| Setting | What it's for | Placeholder value |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Connects to the PostgreSQL database | `Host=localhost;Port=5432;Database=thrive;Username=postgres;Password=postgres` |
| `Admin:Username` | The admin account's username | `admin` |
| `Admin:Password` | The admin account's password | `choose-a-password` |
| `Payment:AccountHolder` | Shown to clients paying by EFT | `Thrive Wellness Pilates` |
| `Payment:Bank` | Shown to clients paying by EFT | `Example Bank` |
| `Payment:AccountNumber` | Shown to clients paying by EFT | `0000000000` |
| `SendGridApiKey` | Authenticates with SendGrid to send email | `SG.your-api-key` |
| `SendGridFromEmail` | The address emails are sent from | `no-reply@example.com` |
| `AppBaseUrl` | Used to build absolute links in emails, such as the cancellation link | `https://localhost:7291` |

No default admin account exists. `Admin:Password` must be set, or the app
starts without creating or updating an admin account and nobody can log in.

### Migrate and run

```
dotnet ef database update --project ThriveWellness
dotnet run --project ThriveWellness
```

## Tests

```
dotnet test
```

This runs 34 tests, covering six service classes: `AdminSeeder`,
`BookingService`, `NotificationService`, `PaymentService`, `SessionService`
and `WaitlistService`. Controllers and the repositories' query shapes are
not covered yet (see Known limitations).
