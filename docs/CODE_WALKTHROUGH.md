# Code walkthrough

This doc exists to let you explain any part of this codebase in plain language
and justify why it was built the way it was - not to be read end to end
before a presentation, but to be the thing you check the night before, or
flip to mid-conversation when a lecturer asks "wait, how does X actually
work?"

Everything in here was confirmed against the actual code while writing it,
not written from memory of what the code "should" do. Where something is a
judgment call rather than a verified fact, it's flagged as such.

## One request, end to end: a new client booking

This traces what happens when someone fills in the booking form for the
first time and submits it, in the order the files are actually touched.

1. **[`Views/Booking/Schedule.cshtml`](../ThriveWellness/Views/Booking/Schedule.cshtml)**
   - the public schedule page. Clicking "Book" on an open session is a link
   to `/Booking/Book/{sessionId}`.
2. **[`Controllers/BookingController.cs`](../ThriveWellness/Controllers/BookingController.cs)**
   `Book(int sessionId)` - loads the session, re-checks server-side that it
   still has room (never trusts that the page the client is looking at is
   still accurate), and shows the email-step form.
3. **[`Views/Booking/Book.cshtml`](../ThriveWellness/Views/Booking/Book.cshtml)**
   - the client types their email and submits, posting to `CheckEmail`.
4. **`BookingController.CheckEmail`** calls
   **[`Services/Implementations/BookingService.cs`](../ThriveWellness/Services/Implementations/BookingService.cs)**
   `CheckClientStatusAsync(email)`, which calls
   **[`Repositories/Implementations/ClientRepository.cs`](../ThriveWellness/Repositories/Implementations/ClientRepository.cs)**
   `GetByEmailAsync` against
   **[`Data/ApplicationDbContext.cs`](../ThriveWellness/Data/ApplicationDbContext.cs)**'s
   `Clients` table. No match, so `IsNew = true` comes back and the full
   intake form (medical notes, consent) is shown.
5. **[`Views/Booking/Details.cshtml`](../ThriveWellness/Views/Booking/Details.cshtml)**
   - the client fills in name, phone, payment type/method, medical notes and
   ticks consent, and submits to `Submit`.
6. **`BookingController.Submit`** runs the cross-field checks data
   annotations can't express (consent required for a new client, payment
   type/method always required), builds a plain `BookingRequest`, and calls
   `BookingService.CreateBookingAsync(request)`.
7. **`BookingService.CreateBookingAsync`**, in order:
   - creates the `Client` row (since none existed) via `ClientRepository.AddAsync`.
   - loads the `Session` via `SessionRepository.GetByIdAsync` and re-checks
     capacity (`BookingRepository.GetBookingsBySessionAsync`) one more time.
   - generates a cancellation token
     ([`Services/CancellationTokenGenerator.cs`](../ThriveWellness/Services/CancellationTokenGenerator.cs))
     and creates the `Booking` row, status `"Awaiting Payment"`, via
     `BookingRepository.CreateBookingAsync`.
   - creates the matching `Payment` row (status `"Pending"`) via
     `PaymentRepository.CreateAsync`.
   - creates the `IntakeForm` row directly against `ApplicationDbContext`
     (new client only).
   - calls
     **[`Services/Implementations/NotificationService.cs`](../ThriveWellness/Services/Implementations/NotificationService.cs)**
     `SendConfirmationEmailAsync(booking)`.
8. **`NotificationService.SendConfirmationEmailAsync`** gathers the client,
   session, location and payment, builds the HTML inline, and calls
   **[`Services/Implementations/EmailSender.cs`](../ThriveWellness/Services/Implementations/EmailSender.cs)**
   `SendEmailAsync`, which is the one place that actually talks to SendGrid.
   A `Notification` row is logged afterwards.
9. Control returns up through `CreateBookingAsync` to `Submit`, which
   redirects to `Confirmation(bookingId)`.
10. **`BookingController.Confirmation`** re-reads the booking, client,
    session, location and payment to build a `BookingConfirmationViewModel`,
    rendered by
    **[`Views/Booking/Confirmation.cshtml`](../ThriveWellness/Views/Booking/Confirmation.cshtml)**.

The booking is still only "Awaiting Payment" at this point - payment
confirmation is a separate, admin-triggered flow (see the feature map below).

## Feature maps

### Booking (a new or returning client books a session)

**Files:** `Controllers/BookingController.cs` (`Book`, `CheckEmail`, `Submit`,
`Confirmation`) → `Services/Implementations/BookingService.cs`
(`CheckClientStatusAsync`, `CreateBookingAsync`) →
`Repositories/Implementations/{Client,Session,Booking,Payment}Repository.cs` →
`Data/ApplicationDbContext.cs` → `Services/Implementations/NotificationService.cs`
→ `Services/Implementations/EmailSender.cs`.

**Steps:** email first, to decide new vs. returning → full or short form
depending on that → server re-validates capacity and consent → booking +
payment (+ intake form) created → confirmation email sent → confirmation page
shown.

**Why built this way:** splitting email-check from the rest of the form
avoids making a returning client re-type their name and phone number every
time, and means the intake/consent fields only ever render for someone who
actually needs to see them.

### Returning-client detection

**Files:** `BookingController.CheckEmail` →
`BookingService.CheckClientStatusAsync` → `ClientRepository.GetByEmailAsync`.

**Steps:** the submitted email is looked up in `Clients.Email`; a match
returns their stored name/phone and `IsNew = false` to pre-fill the form,
no match returns `IsNew = true`.

**Why built this way:** email is the only identifier a client has, since
there's no account/login system for clients - it has to double as both
"who is this" and "how do I reach them".

### Payment confirmation

**Files:** `Controllers/PaymentController.cs` (`Confirm`) →
`Services/Implementations/PaymentService.cs` (`ConfirmPaymentAsync`, and its
`PaymentConfirmed` event) → wired in `Program.cs` → `NotificationService.OnPaymentConfirmed`
→ `NotificationService.SendWelcomeEmailAsync` → `EmailSender.SendEmailAsync`.

**Steps:** an admin clicks "Confirm" on the Payments page → payment status
becomes `"Confirmed"`, booking status becomes `"Confirmed"` → `PaymentConfirmed`
fires → `NotificationService` (subscribed from `Program.cs`, not from its own
constructor) sends the welcome email.

**Why built this way:** this is the Observer pattern - `PaymentService`
doesn't know or care that an email gets sent; it just reports "a payment was
confirmed" and whatever is listening (today, just the welcome email) reacts.
See the Design Decisions section for why the subscription itself had to move
out of `NotificationService`'s constructor.

### Waitlist promotion

**Files:** `Services/Implementations/WaitlistService.cs`
(`JoinWaitlistAsync`, `PromoteNextInLineAsync`) → `Repositories/Implementations/WaitlistRepository.cs`
→ triggered from either `BookingService.CancelAsync` (a cancellation) or
`SessionService.MarkAsOpenAsync` (an admin reopening a session).

**Steps:** joining adds an entry at `Max(Position) + 1`; promoting checks the
session actually has room (see the capacity guard, below), takes the
lowest-`Position` entry, turns it into a real `Booking`, and shifts everyone
behind them up one position.

**Why built this way:** a plain FIFO queue keyed by an integer `Position` is
the simplest structure that supports "who's next" and "close any gap a
removal leaves" without needing a linked list or a separate ordering table.

### Cancellation by link

**Files:** `BookingController.Cancel` (no `[Authorize]`) →
`BookingService.CancelBookingAsync` → `BookingRepository.GetByCancellationTokenAsync`
→ `CancellationTokenGenerator` (where the token was originally generated).

**Steps:** the email's cancel link is `/Booking/Cancel/{token}`; the booking
is looked up by that token (not an id), flipped to `"Cancelled"`, and the
freed slot triggers `WaitlistService.PromoteNextInLineAsync`.

**Why built this way:** a client never logs in, so there's no session or
cookie to authorize this action with - the cryptographically random token
*is* the credential. See Design Decisions for the full reasoning.

### Scheduled reminder and location emails

**Files:** `Services/Implementations/ScheduledNotificationHostedService.cs`
(the timer) → `Services/Implementations/ScheduledNotificationService.cs`
(`SendDueRemindersAsync`, `SendDueLocationEmailsAsync`) →
`NotificationService.SendReminderEmailAsync` / `SendLocationEmailAsync`.

**Steps:** a `BackgroundService` runs once on startup and then every hour;
each run queries for Confirmed bookings meeting the day-before (reminder) or
same-day-and-new-client (location) condition, and skips any booking that
already has a matching `Notification` row logged.

**Why built this way:** checking `Notifications` for an existing row (rather
than, say, a boolean flag on `Booking`) means the hourly re-run is naturally
idempotent - running it twice in the same hour, or missing a run and
catching up an hour late, can't double-send.

### Admin login

**Files:** `Controllers/AccountController.cs` (`Login`) →
`Services/Implementations/AuthService.cs` (`Login`) → ASP.NET Core cookie
authentication, configured in `Program.cs`.

**Steps:** username/password posted → `AuthService.Login` looks the admin up
by username and checks the password with `BCrypt.Net.BCrypt.Verify` →
success issues a cookie carrying the username as its one claim → every
`[Authorize]`-attributed controller checks only for that cookie's presence,
nothing more granular.

**Why built this way:** there's exactly one admin account by design (no
sign-up, no roles), so a single cookie claim is all authorization ever needs
to check.

## Design decisions with justification

- **MVC.** ASP.NET Core's built-in pattern; controllers stay thin
  (orchestration + validation only), views are server-rendered Razor, and
  models/view models carry data between the two. Chosen because this is a
  traditional server-rendered site with no need for a separate API +
  frontend split.
- **The service layer.** Every business rule (capacity checks, pricing,
  consent requirements, the new-vs-returning distinction, cancellation
  rules) lives in a `Services/Implementations/*Service.cs` class, never in a
  controller. This means the same rule can't drift between two controllers
  that both need it, and the rule can be unit tested without spinning up
  MVC at all (see `ThriveWellness.Tests`).
- **The Repository pattern.** Every table has a thin `*Repository` wrapping
  the `DbSet`, behind an interface. Services depend on the interface, not
  `ApplicationDbContext` directly. See "Why Repository instead of using the
  DbContext directly?" below.
- **The Observer pattern.** `IPaymentService.PaymentConfirmed` is the
  subject; `NotificationService.OnPaymentConfirmed` is the observer. The
  subscription is registered as a DI factory step in `Program.cs` rather
  than in either class's constructor - see the circular-dependency bug in
  the next section for exactly why that matters.
- **Dependency injection.** Every service and repository is registered
  `Scoped` (see `Program.cs`), matching `ApplicationDbContext`'s own
  lifetime - one instance of each per HTTP request, so everything touched
  by a single request shares one database connection/transaction context.
- **PostgreSQL.** Chosen as the relational database, hosted on Railway,
  accessed via Npgsql/EF Core. A relational database fits this domain well
  - clients, sessions, bookings, payments and waitlist entries are all
  related rows with real foreign keys between them, not documents.
- **The timestamp choice (`timestamp without time zone`).** None of the
  `DateTime` values in this schema carry real timezone meaning (a session's
  date/time is just "this local time, at this studio"), so every `DateTime`
  column is mapped to Postgres's `timestamp without time zone` type via a
  value converter in `ApplicationDbContext.OnModelCreating`, which also
  normalizes every value's `DateTimeKind` to `Unspecified` on the way in and
  out - Npgsql is strict about `Kind` matching the column type. This was
  originally the other way around (`timestamptz`, expecting `Kind=Utc`
  everywhere) and caused real write failures - see Bugs Found and Fixed.
- **Synchronous notification sending.** `PaymentService.ConfirmPaymentAsync`
  blocks on `NotificationService.OnPaymentConfirmed`'s whole async chain,
  including the real SendGrid call, before returning. This was a deliberate
  trade after a bug (see below) where a detached fire-and-forget task raced
  the request's own `DbContext` disposal. It's marked in the code as a
  known thing to revisit: the right long-term fix is a queued/background
  send with its own scope, not blocking the admin's request on SendGrid's
  response time.
- **Token-based cancellation.** A client cancels via a link
  (`/Booking/Cancel/{token}`) carrying a token from
  `CancellationTokenGenerator.Generate()` - 32 bytes from
  `RandomNumberGenerator` (a CSPRNG), base64url-encoded. With no client
  login, this token is the entire security boundary for that action: it has
  to be unguessable, not just unique.
- **Docker, with Render, Railway and SendGrid.** The app is containerized
  (see `ThriveWellness/Dockerfile`) and deployed to Render, which builds and
  runs that container and assigns the listening port via the `PORT`
  environment variable at container start (`Program.cs` reads it and only
  overrides the default URL binding when it's set, so local development is
  unaffected). The database is a separate managed PostgreSQL instance on
  Railway, connected to over the network via the `DefaultConnection`
  string. Outbound email goes through SendGrid's API rather than SMTP
  directly, since Render (like most PaaS hosts) blocks outbound SMTP ports.

## Bugs found and fixed

These are real bugs from this project's own history, not hypothetical
examples - each one actually happened, was diagnosed, and was fixed in a
specific commit.

1. **Circular DI dependency deadlocked every request.**
   *Symptom:* every request touching `IPaymentService` or
   `INotificationService` hung indefinitely.
   *Cause:* `NotificationService` depended on `IPaymentService` in its own
   constructor (to subscribe to `PaymentConfirmed`), while `IPaymentService`'s
   DI factory resolved `INotificationService` to force that subscription to
   exist - a genuine cycle the built-in container doesn't catch for
   factory-based registrations, so it deadlocked on the scope's internal
   lock instead of throwing.
   *Fix:* removed the constructor dependency; the `+=` subscription now
   happens as a step inside `Program.cs`'s DI registration instead.
   *File:* `Program.cs`, `Services/Implementations/NotificationService.cs`.

2. **Fire-and-forget notification handler raced its own request scope.**
   *Symptom:* confirming a payment sometimes silently skipped sending the
   welcome email, with "another read operation is pending" / connection-
   aborted errors in the log.
   *Cause:* `OnPaymentConfirmed` discarded the async handler's `Task`
   (`_ = HandlePaymentConfirmedAsync(e)`), letting it run detached while
   still sharing the request's Scoped `DbContext` - a race against that same
   request's own completion (and the `DbContext` disposal that follows it).
   *Fix:* block synchronously instead (`.GetAwaiter().GetResult()`) so the
   handler finishes before the request does.
   *File:* `Services/Implementations/NotificationService.cs`.

3. **`PromoteNextInLineAsync` could overbook a session.**
   *Symptom:* reopening a session that was still effectively full could push
   a waitlisted client into a booking beyond the session's capacity.
   *Cause:* unlike a cancellation (which always frees exactly one slot),
   reopening a session doesn't guarantee a slot actually freed up, and the
   promotion logic didn't check.
   *Fix:* added a capacity check (current active bookings vs. `Capacity`)
   before promoting; if there's no room, the entry stays queued.
   *File:* `Services/Implementations/WaitlistService.cs`.

4. **`DateTime` writes failing against the database.**
   *Symptom:* `DbUpdateException` when creating a booking (and, earlier, when
   creating a session).
   *Cause:* Npgsql 8 requires a `DateTime`'s `Kind` to match the column type
   exactly - `Kind=Utc` for `timestamptz`, `Kind=Unspecified` for
   `timestamp without time zone` - but this schema's values are naive local
   times with no real timezone meaning, constructed inconsistently
   (`DateTime.Now`, `DateTime.UtcNow`, form input) across the codebase.
   *Fix:* mapped every `DateTime`/`DateTime?` column to
   `timestamp without time zone` and added a value converter that normalizes
   every read/write to `Kind=Unspecified`, rather than patching each call
   site (and the next one, and the one after that).
   *File:* `Data/ApplicationDbContext.cs`.

5. **Booking routes 404'd.**
   *Symptom:* `/Booking/Book/{sessionId}`, `/Booking/Confirmation/{bookingId}`
   and `/Booking/Cancel/{token}` all returned 404.
   *Cause:* the app's only route is the default conventional route
   (`{controller}/{action}/{id?}`), which only binds a trailing URL segment
   to a parameter literally named `id` - these actions take `sessionId`,
   `bookingId` and `token`.
   *Fix:* added explicit `[HttpGet("Booking/Book/{sessionId:int}")]`-style
   attribute routes to each of those actions.
   *File:* `Controllers/BookingController.cs`.

6. **Create Session's date defaulted to year 0001.**
   *Symptom:* the Create Session form's date field loaded already showing
   `0001-01-01`, and submitting it unchanged failed validation.
   *Cause:* `SessionFormViewModel.Date` is a non-nullable `DateTime`, so
   leaving it unset in the `Create` GET action left it at
   `DateTime.MinValue` - which also happens to fail `SessionService`'s own
   past-date check.
   *Fix:* default it to `DateTime.Today.AddDays(1)` in the GET action, a
   valid starting point an admin can change.
   *File:* `Controllers/SessionController.cs`.

7. **An unstyled close button leaked onto the desktop header.**
   *Symptom:* a bare, browser-default-bordered X button appeared between the
   logo and the nav links at desktop widths.
   *Cause:* `.thrive-nav__close-row` (the mobile dropdown's in-panel close
   control) only had a `display` rule inside the `@media (max-width: 768px)`
   block - nothing hid it above that breakpoint, unlike the hamburger toggle
   next to it, which already had a `display: none` base rule.
   *Fix:* added the same `display: none` base rule, matching the toggle's
   existing pattern.
   *File:* `wwwroot/css/thrive.css`.

## Questions a lecturer might ask

**How is double booking prevented?**
`BookingService.CreateBookingAsync` counts active (non-cancelled) bookings
against the session and compares to `Capacity` immediately before creating
a new one, inside the same request - so even if the schedule page a client
is looking at is stale, the server re-checks at the moment of booking rather
than trusting the client.

**How are passwords stored?**
Hashed with BCrypt (`AuthService.HashPassword`), which generates and embeds
its own random salt per hash. The plaintext password is never stored, and
`AuthService.Login` verifies via `BCrypt.Net.BCrypt.Verify` rather than
re-hashing and comparing strings.

**What stops CSRF?**
Every state-changing POST action has `[ValidateAntiForgeryToken]`, and
every form that posts to one includes `@Html.AntiForgeryToken()` (or the
tag helper equivalent via `asp-controller`/`asp-action`), which is ASP.NET
Core's built-in anti-forgery token pair.

**What stops SQL injection?**
Every query goes through EF Core (LINQ or parameterized raw SQL never
appears in this codebase) - EF Core parameterizes every value it sends to
Npgsql, so user input is never concatenated into a SQL string.

**What happens if SendGrid fails?**
`EmailSender.SendEmailAsync` throws an `InvalidOperationException` on any
non-success response rather than swallowing it. Because
`NotificationService`'s send is currently synchronous and awaited inline
(see the Design Decisions entry on synchronous sending), that exception
propagates up through whatever triggered the email - for a booking, that
means `CreateBookingAsync` itself would fail, and the client would see an
error instead of a silent missing email. This is arguably too strict for a
non-critical failure like an email, and is one of the things worth
revisiting (see Known Limitations).

**How does a deploy happen?**
A push to `main` triggers `.github/workflows/deploy.yml`, which first reuses
`ci.yml`'s build-and-test job (`workflow_call`) as a gate, and only if that
passes, POSTs to a Render deploy hook URL stored as the
`RENDER_DEPLOY_HOOK_URL` repository secret. Render then builds the
`Dockerfile` and redeploys the container.

**Why Repository instead of using the DbContext directly?**
Three reasons that showed up in practice: it keeps every query for a table
in one place instead of scattered across services; it means a service's
unit tests (see `ThriveWellness.Tests`) can depend on an interface rather
than needing a real or in-memory database; and it's the layer that would
need to change if the data access technology ever did, without touching any
business rule.

**How does returning-client detection work?**
By email lookup alone (`ClientRepository.GetByEmailAsync`) - there's no
login for clients, so email is the only identifier available, and it
doubles as the contact address emails get sent to.

**What would you improve next?**
Honestly, in rough priority order: move the SendGrid send off the request's
critical path (a queue/background worker, per the existing TODO in
`PaymentService`); add controller- and repository-level tests (today's 30
tests cover five service classes only - `AdminSeeder`, `BookingService`,
`PaymentService`, `SessionService`, `WaitlistService` - not the controllers
or the EF Core query shapes in the repositories); and reconsider whether a
SendGrid failure should really fail the whole booking request rather than
just logging it and letting the booking go through without its email.

**Why does a client cancel via a link instead of logging in?**
There's no client account system at all - a client only ever interacts
with the site anonymously, identified by email. Building a full login flow
just for cancellation would be a lot of extra surface area for one action,
so a single-purpose, unguessable token does the same job more simply.

**Is the cancellation token really single-use?**
Not by invalidating the token itself - by what it points at. Once used, the
booking's `Status` becomes `"Cancelled"`, and
`BookingService.CancelAsync` refuses to cancel an already-cancelled
booking, so reusing the same link just reports "already cancelled" instead
of doing anything further.

**Why is the admin dashboard split into several separate queries instead of
one big join?**
`AdminDashboardService.GetDashboardAsync` runs the counts, the upcoming
sessions, the pending-payments preview and the recent bookings as
independent queries because each one has a genuinely different shape (some
are simple counts, others are joined projections) - forcing them into one
query would mean either a much more complex join or fetching data the page
doesn't need.

**Why does `WaitlistService` depend on `IBookingRepository` instead of
`IBookingService`?**
`BookingService` already depends on `IWaitlistService` (to promote the
waitlist on a cancellation); if `WaitlistService` depended on
`IBookingService` back, that would be a circular dependency between the two
services. Depending on the repository instead breaks the cycle, since
`BookingRepository` doesn't depend on either service.

**What's the difference between a session being "closed" and being "full"?**
`Session.IsOpen` is an admin's manual switch (`MarkAsFullAsync`/
`MarkAsOpenAsync`); "full" (`IsFull` on the relevant view models) means
either that switch is off *or* the active booking count has reached
`Capacity`. A session can be full without being manually closed, and an
admin can manually close a session that still has open spots.

**Why does `AdminSeeder` compare password hashes instead of just re-hashing
on every startup?**
BCrypt embeds a new random salt on every call, so re-hashing the configured
password on every deploy would produce a different hash each time even when
the password hasn't actually changed, making every deploy look like a
password rotation in the data. `AdminSeeder` only writes a new hash when
`BCrypt.Net.BCrypt.Verify` shows the configured password genuinely doesn't
match what's already stored.

## Known limitations

Stated plainly, as they actually are today:

- **Free-tier cold start.** Render's free tier spins the container down
  after a period of inactivity, so the first request after a while can take
  noticeably longer while it starts back up.
- **Gmail spam placement.** Confirmation and reminder emails sent via
  SendGrid have, in practice, sometimes landed in Gmail's spam folder rather
  than the inbox - a known deliverability consideration with transactional
  email from a newer sending domain, not something the application code
  controls.
- **A single admin account.** There's no multi-admin support, no roles, and
  no permission granularity - `AdminSeeder` manages exactly one account, and
  `[Authorize]` only ever checks "is someone signed in", never "is this
  admin allowed to do this specific thing".
- **No online payments.** `BookingService` records a payment as "Pending"
  immediately and an admin manually marks it "Confirmed" after an EFT or
  cash payment arrives by some other channel - there's no payment gateway
  integration.
- **Synchronous email send.** As covered in Design Decisions, confirming a
  payment (and creating a booking) currently waits on the full SendGrid
  round-trip before returning a response to the admin or client, rather than
  queuing the send in the background.
