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

- Google sign-in at `/Account/ClientLogin`. Booking, the waitlist and the dashboard need a signed-in client. A first sign-in links to an existing client with the same verified email, so clients who booked before sign-in existed keep their history; otherwise a new client is created.
- A schedule that can be browsed week by week, from the current week up to the last scheduled class, filtered by location or date.
- A booking form pre-filled from the client's account. An intake form with medical notes and consent is shown only on a client's first booking.
- Payment by card online, or by EFT or cash confirmed by an admin (see Payments below).
- A waitlist with automatic promotion when a spot opens up.
- A client dashboard (`/Account/Dashboard`) with stat tiles (upcoming classes, classes attended, total spent, current streak), upcoming bookings with Pay now and Cancel buttons, payments and receipts, past bookings and waitlist entries.
- Rewards: a weekly streak (current and longest) and eight badges, worked out by `RewardsService` from attended classes. The badges are First Class (1 class), Getting Going (5), Committed (10), Dedicated (25), Three Week Streak, Eight Week Streak, Both Studios and Early Bird (a class before 09:00). Badges are never taken away once earned.
- Cancelling from the dashboard, for the client's own booking and only before the class starts. A card payment is refunded automatically; an EFT or cash payment is marked Refund due for the studio to pay back.
- Cancellation through a single-use link in the booking emails, which works without signing in.
- A printable receipt for every card payment (`/Checkout/Receipt/{id}`), also linked from the dashboard.

### Payments

- Prices: R120 per class, or R450 for a monthly pass.
- **Card.** The booking goes straight to a checkout page (`/Checkout/Pay/{bookingId}`). `CheckoutService` charges the card through the `IPaymentGateway` interface, records every attempt as a `PaymentTransaction`, and on success confirms the payment and shows the receipt. The registered gateway is `SimulatedCardGateway`: it checks the card the way a real provider would (Luhn check, expiry, CVC) and answers the standard test cards (4242 4242 4242 4242 and 5555 5555 5555 4444 succeed, 4000 0000 0000 0002 is declined, 4000 0000 0000 9995 has insufficient funds), but no bank is contacted and no money moves. The card number, expiry and CVC are never stored or logged; only the brand, the last four digits and the gateway reference are kept. Moving to a real provider such as PayFast or Stripe means registering its own `IPaymentGateway` in `Program.cs`.
- **EFT and cash.** The booking is held as Awaiting Payment, the booking received email gives the studio's EFT details, and an admin presses Confirm on the Payments page once the money arrives.
- **Refunds.** When a paid booking is cancelled, `RefundService` refunds a card payment through the gateway and marks it Refunded. An EFT or cash payment is marked Refund due, and the admin presses Mark refunded once it has been paid back by hand.
- The admin Payments page has tabs for All, Pending, Confirmed, Refund due and Refunded.

### Admin

The admin sidebar has Dashboard, Bookings Calendar, Payments, Revenue, Sessions, Locations, Clients, Waitlist and Emails.

- **Dashboard:** booking and client counts, pending payments with a Confirm button, upcoming sessions and recent bookings. A Send reminders now button runs the day-before reminder check on demand and reports how many were sent, skipped or failed.
- **Bookings Calendar:** a month view of every session with its booking count, moved month by month. Clicking a session opens its edit page.
- **Payments:** the tabs described above, Confirm for EFT and cash, and Mark refunded for Refund due payments.
- **Revenue** (`RevenueService`): net revenue (Confirmed payments only, refunds shown separately and never counted), paid count, average per payment and total refunded. The date range can be all time, this month, the last 30 days, this year or custom dates. It shows a weekly or monthly line graph and breakdowns by month, payment method, payment type and venue, plus a searchable spend-per-client table that downloads as CSV.
- **Sessions:** create, edit, delete, mark full or open, and per session an Attendees list (where a booking can be marked as a no-show once the class has started) and a Waitlist list.
- **Locations:** create, edit and delete studios.
- **Clients:** every client with their classes attended, streak and badges, with search and sorting by classes attended.
- **Waitlist:** every waitlist entry across sessions, with Notify (a courtesy email), Add to class and Remove.
- **Emails:** the latest 50 emails to clients, with their Sent or Failed status, filterable by type (booking received, payment confirmed, reminder, location, off the waitlist, cancellation).

### Automated emails

- A booking received email when a booking is created. It holds the spot pending payment and never says the booking is confirmed. For a card booking it links to the checkout page; for EFT or cash it gives the studio's banking details and a payment reference.
- A payment confirmed email when a payment is confirmed, either by an admin (EFT or cash) or automatically after a successful card payment.
- A scheduled reminder email, sent the day before a session.
- A scheduled location email, sent on the day of a session to clients still marked as new.
- An off the waitlist email when a waitlisted client is moved into a class, and a courtesy email when an admin presses Notify on a waitlist entry.
- A cancellation link in the booking emails that frees the slot and promotes the next person on the waitlist.

The scheduled emails run from a background service once at startup and then every hour. Every send is logged in the `Notifications` table as Sent or Failed, which stops the hourly run from sending the same email twice and lets a failed reminder or location email be retried on the next run. The admin Emails page shows the failures. A failed email never undoes a booking, a cancellation or a payment confirmation.

## Tech stack

| Area | Technology |
|---|---|
| Framework | ASP.NET Core MVC, .NET 8 |
| Data access | Entity Framework Core 8, Npgsql |
| Database | PostgreSQL, hosted on Neon |
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
- Docker, for the local development database (or any PostgreSQL install)
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
| `SendGridApiKey` | Authenticates with SendGrid to send email | `your-sendgrid-api-key` |
| `SendGridFromEmail` | The address emails are sent from | `no-reply@example.com` |
| `AppBaseUrl` | Used to build absolute links in emails, such as the cancellation link | `https://localhost:7291` |

No default admin account exists. `Admin:Password` must be set, or the app
starts without creating or updating an admin account and nobody can log in.

### Local development

Local development uses a throwaway PostgreSQL in Docker, never the live
database. Start it with:

```
docker compose -f docker-compose.dev.yml up -d
```

It listens on port 5433, so point `DefaultConnection` at
`Host=localhost;Port=5433;Database=thrive_dev;Username=postgres;Password=devpass`,
then migrate and run the app as below. The live database is only touched by
the demo reset script and by deploys.

### Migrate and run

```
dotnet ef database update --project ThriveWellness
dotnet run --project ThriveWellness
```

## Tests

```
dotnet test
```

This runs more than 400 xUnit tests (with Moq and the EF Core in-memory
provider). They cover:

- Services: `AdminSeeder`, `AuthService`, `BookingService`,
  `CheckoutService`, `ClientAccountService`, `ClientDashboardService`,
  `NotificationService`, `PaymentService`, `RefundService`,
  `RevenueService`, `RewardsService`, `ScheduledNotificationService`,
  `SessionService`, `SimulatedCardGateway` and `WaitlistService`.
- Rule helpers: `Attendance`, `BadgeRules`, `Streaks`, `ScheduleWeek`,
  `Money`, `ChartScale`, `RevenueChart`, `ClientSpendCsv`, `HeaderNav` and
  `StartupSeeding`.
- Authorization: every controller action is found by reflection and checked
  against the real policies, so a new admin page that forgets its
  protection fails a test.
- Email failures: a failed send never loses a booking, a cancellation or a
  payment confirmation, and is logged as Failed.
- Card data: tests fail if the card number, expiry or CVC is ever stored on
  any table.
- Phone number validation on the booking form.

Not covered: `AdminDashboardService`, `LocationService`, `EmailSender` (the
real SendGrid call) and `ScheduledNotificationHostedService`; the
controllers' action logic (only their authorization is tested); the views;
and the repositories' queries against a real PostgreSQL database (tests use
the in-memory provider, which does not behave exactly like PostgreSQL).

## CI/CD and deployment

Two GitHub Actions workflows:

- **`ci.yml`** runs on every push to any branch, and on pull requests
  targeting `develop` or `main`. It restores, builds, and runs
  `dotnet test`. It also declares `workflow_call`, so `deploy.yml` can reuse
  this exact job instead of duplicating it.
- **`deploy.yml`** runs on every push to `main`. It first calls `ci.yml` as
  a gate; if that passes, it POSTs to a Render deploy hook URL, stored as
  the `RENDER_DEPLOY_HOOK_URL` repository secret.

Render builds and runs the app from `ThriveWellness/Dockerfile` (a two-stage
build: the SDK image builds and publishes the app, then the ASP.NET runtime
image runs it). Render assigns the container's listening port through the
`PORT` environment variable, which `Program.cs` reads at startup.

Render environment variable names (values are set in the Render dashboard,
not in the repository):

- `PORT` (set by Render itself)
- `ConnectionStrings__DefaultConnection`
- `Admin__Username`
- `Admin__Password`
- `Payment__AccountHolder`
- `Payment__Bank`
- `Payment__AccountNumber`
- `SendGridApiKey`
- `SendGridFromEmail`
- `AppBaseUrl`

## Branching

Work was built as a series of feature branches, merged into `main`:

- `feature/project-scaffold`
- `feature/session-management`
- `feature/booking-system`
- `feature/auth`
- `feature/payment-tracking`
- `feature/email-service`
- `feature/waitlist`
- `feature/location-management`
- `feature/admin-dashboard`
- `feature/admin-design`
- `feature/frontend-design`
- `feature/dockerfile`
- `feature/ci-cd`
- `feature/unit-tests`
- `feature/eft-details`
- `feature/comments`
- `feature/cleanup`
- `fix/desktop-menu-close-button`

## Security

- Passwords are hashed with BCrypt, which generates and embeds its own random salt. The plaintext password is never stored.
- The admin login locks for 15 minutes after 5 failed attempts in a row. Unknown usernames get the same messages and the same timing, so the login page never reveals whether a username exists.
- Every state-changing form includes an anti-forgery token, and every matching controller action validates it.
- All database queries go through Entity Framework Core, which parameterises every value. No raw SQL is built from user input.
- Session cookies and form posts are expected to run over HTTPS in production.
- Cancellation uses a cryptographically random, single-use token rather than a login, since clients never create an account.
- Real credentials and connection strings are kept out of the repository, in `appsettings.Development.json` (gitignored) locally and in environment variables on Render.

## Known limitations

- **Free-tier cold start.** The hosting tier spins the container down after a period of inactivity, so the first request after a while is slower.
- **Gmail spam placement.** Emails sometimes land in Gmail's spam folder rather than the inbox, because the sending domain is not authenticated.
- **A single admin account.** There is no multi-admin support and no roles.
- **No online payments.** EFT and cash payments are confirmed manually by the admin; there is no payment gateway integration.
- **Emails are sent synchronously.** A booking or payment confirmation waits on the full email send before returning a response. A background queue is the planned next step.
- **No custom domain.** A custom domain was recommended in Task 1 but is not set up.
- **Returning-client identification.** Returning clients are identified by email only, so anyone who knows a client's email can see and change their name and phone. A verification step, such as an emailed code, is the next improvement.

## Documentation and design

- [docs/CODE_WALKTHROUGH.md](docs/CODE_WALKTHROUGH.md): a full walkthrough of how each feature works, the design decisions behind it, and bugs found and fixed.
- [Figma desktop prototype](https://www.figma.com/community/file/1671168701070686139/insy7315-desktop-interactable-prototype)
- [Figma mobile prototype](https://www.figma.com/community/file/1671168418793571256/insy7315-mobile-interactiveprototype)

The Task 1 documentation was submitted separately.

## Admin access

Username: `admin`. The password is set through the `Admin__Password`
setting and is provided in class or on request.
