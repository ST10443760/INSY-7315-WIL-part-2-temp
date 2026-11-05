# Code walkthrough

This doc exists to let you explain any part of this codebase in plain language
and justify why it was built the way it was - not to be read end to end
before a presentation, but to be the thing you check the night before, or
flip to mid-conversation when a lecturer asks "wait, how does X actually
work?"

Everything in here was confirmed against the actual code while writing it,
not written from memory of what the code "should" do. Where something is a
judgment call rather than a verified fact, it's flagged as such.

## One request, end to end: a client's first booking, paid by card

This traces what happens when a signed-in client books their first class
and pays by card, in the order the files are actually touched.

1. **[`Views/Booking/Schedule.cshtml`](../ThriveWellness/Views/Booking/Schedule.cshtml)**
   is the public schedule. "Book Now" on an open session is a link to
   `/Booking/Book/{sessionId}`. That action has
   `[Authorize(Policy = AuthPolicies.ClientOnly)]`, so a visitor who is not
   signed in is sent to `/Account/ClientLogin` with a `returnUrl` (see
   `OnRedirectToLogin` in `Program.cs`) and comes back here after Google
   sign-in.
2. **[`Controllers/BookingController.cs`](../ThriveWellness/Controllers/BookingController.cs)**
   `Book(int sessionId)` loads the session and re-checks server-side that it
   is open and still has room (a full session redirects to `JoinWaitlist`).
   It loads the client from the `ClientId` claim in the sign-in cookie and
   shows the form pre-filled with their name and phone.
3. **[`Views/Booking/Details.cshtml`](../ThriveWellness/Views/Booking/Details.cshtml)**
   shows the medical notes and consent fields only when
   `BookingService.IsFirstBookingAsync` says the client has never booked.
   The client picks a payment plan and method (Card) and submits to `Submit`.
4. **`BookingController.Submit`** works out "first booking" again on the
   server (never trusting a hidden field), runs the cross-field checks data
   annotations can't express (consent on a first booking, payment plan and
   method required, method must be card, EFT or cash), builds a plain
   `BookingRequest` with the client id from the cookie, and calls
   `BookingService.CreateBookingAsync(request)`.
5. **[`Services/Implementations/BookingService.cs`](../ThriveWellness/Services/Implementations/BookingService.cs)**
   `CreateBookingAsync`, in order:
   - loads the `Client` via `ClientRepository.GetByIdAsync` and saves any
     edit to their name or phone (and, on a first booking, their payment
     plan).
   - loads the `Session` and re-checks capacity
     (`BookingRepository.GetBookingsBySessionAsync`). If it filled up in the
     meantime, it returns `RequiresWaitlist` and `Submit` adds the client to
     the waitlist instead.
   - generates a cancellation token
     ([`Services/CancellationTokenGenerator.cs`](../ThriveWellness/Services/CancellationTokenGenerator.cs))
     and creates the `Booking` row, status `"Awaiting Payment"`.
   - creates the matching `Payment` row (status `"Pending"`, amount from
     `PaymentPricing`: R120 per class or R450 monthly).
   - on a first booking, creates the `IntakeForm` row directly against
     `ApplicationDbContext`.
   - calls
     **[`Services/Implementations/NotificationService.cs`](../ThriveWellness/Services/Implementations/NotificationService.cs)**
     `SendConfirmationEmailAsync(booking)` inside a `try`, so a failed email
     is logged but never undoes the booking.
6. **`NotificationService.SendConfirmationEmailAsync`** builds the booking
   received email (for a card booking it has a Pay now link instead of
   banking details) and sends it through
   **[`Services/Implementations/EmailSender.cs`](../ThriveWellness/Services/Implementations/EmailSender.cs)**
   `SendEmailAsync`, the one place that talks to SendGrid. A `Notification`
   row is logged as Sent or Failed.
7. Back in `Submit`, a card booking redirects to
   **[`Controllers/CheckoutController.cs`](../ThriveWellness/Controllers/CheckoutController.cs)**
   `Pay(bookingId)`, which shows
   **[`Views/Checkout/Pay.cshtml`](../ThriveWellness/Views/Checkout/Pay.cshtml)**.
   (An EFT or cash booking goes to `Confirmation(bookingId)` instead and
   waits for an admin.)
8. The client enters a card and posts it. **`CheckoutController.Pay`** hands
   it to
   **[`Services/Implementations/CheckoutService.cs`](../ThriveWellness/Services/Implementations/CheckoutService.cs)**
   `PayAsync`, which:
   - checks the booking belongs to this client and is still unpaid.
   - refuses a second attempt on the same payment while one is in flight.
   - if an earlier charge already succeeded but its confirmation did not
     finish, confirms it without charging again.
   - otherwise calls `IPaymentGateway.ChargeAsync`, here
     **[`Services/Implementations/SimulatedCardGateway.cs`](../ThriveWellness/Services/Implementations/SimulatedCardGateway.cs)**,
     and records the attempt as a `PaymentTransaction` (Succeeded or
     Declined, with brand, last four digits and gateway reference only).
   - on success calls `PaymentService.ConfirmPaymentAsync`, which marks the
     payment and booking Confirmed and raises `PaymentConfirmed`, so
     `NotificationService` sends the payment confirmed email.
9. The controller redirects to `Receipt(paymentId)`, rendered by
   **[`Views/Checkout/Receipt.cshtml`](../ThriveWellness/Views/Checkout/Receipt.cshtml)**,
   which has a Print receipt button.

## Feature maps

### Booking (a signed-in client books a session)

**Files:** `Controllers/BookingController.cs` (`Book`, `Submit`,
`Confirmation`) → `Services/Implementations/BookingService.cs`
(`IsFirstBookingAsync`, `CreateBookingAsync`) →
`Repositories/Implementations/{Client,Session,Booking,Payment}Repository.cs` →
`Data/ApplicationDbContext.cs` → `Services/Implementations/NotificationService.cs`
→ `Services/Implementations/EmailSender.cs`.

**Steps:** sign in with Google → form pre-filled from the client record,
with intake and consent fields on a first booking only → server re-checks
capacity and consent → booking, payment (and intake form) created →
booking received email sent → checkout page for card, booking received page
for EFT or cash.

**Why built this way:** the client is identified by their sign-in, not by
anything typed into the form, so a returning client never re-types their
details and nobody can book as someone else by typing their email. "First
booking" is worked out from the client's bookings on the server each time,
so the consent rule can't be skipped by editing the form.

### Client sign-in with Google

**Files:** `Controllers/AccountController.cs` (`ClientLogin`,
`GoogleSignIn`, `GoogleCallback`, `ClientSignOut`) →
`Services/Implementations/ClientAccountService.cs`
(`SignInWithGoogleAsync`) → `Repositories/Implementations/ClientRepository.cs`
→ `Authorization/ClientClaims.cs`, with the Google and cookie schemes set up
in `Program.cs`.

**Steps:** `GoogleSignIn` (a POST with an anti-forgery token) starts the
Google round trip → Google returns to `/signin-google`, which stores the
result in a short-lived External cookie → `GoogleCallback` reads the
profile and calls `SignInWithGoogleAsync`, which refuses an unverified
email, signs in a client already linked by `GoogleSubject`, otherwise links
an existing client with the same email (ignoring case), otherwise creates a
new client → the External cookie is swapped for the normal sign-in cookie
carrying the Client role, the client id, name and email → the client lands
on the `returnUrl` (local URLs only) or their dashboard.

**Why built this way:** Google handles passwords and email verification, so
the app stores no client passwords. Linking by verified email keeps the
history of clients who booked before sign-in existed. If the Google
settings are missing, the Google scheme is never registered and the sign-in
page says sign-in isn't available, instead of the app failing to start.

### Payment confirmation

**Files:** `Controllers/PaymentController.cs` (`Confirm`) →
`Services/Implementations/PaymentService.cs` (`ConfirmPaymentAsync`, and its
`PaymentConfirmed` event) → wired in `Program.cs` → `NotificationService.OnPaymentConfirmed`
→ `NotificationService.SendWelcomeEmailAsync` → `EmailSender.SendEmailAsync`.

**Steps:** an admin clicks "Confirm" on the Payments page for an EFT or
cash payment (or `CheckoutService` calls the same method after a successful
card charge) → payment status becomes `"Confirmed"`, booking status becomes
`"Confirmed"` → `PaymentConfirmed` fires → `NotificationService`
(subscribed from `Program.cs`, not from its own constructor) sends the
payment confirmed email (logged with type `Welcome`). `PaymentService`
catches a failed send, so the payment stays confirmed and the failure shows
on the admin Emails page. `PaymentController` then asks `RewardsService` to
check for newly earned badges.

**Why built this way:** this is the Observer pattern - `PaymentService`
doesn't know or care that an email gets sent; it just reports "a payment was
confirmed" and whatever is listening (today, just the payment confirmed
email) reacts. Card payments reuse it unchanged. See the Design Decisions
section for why the subscription itself had to move out of
`NotificationService`'s constructor.

### Card checkout

**Files:** `Controllers/CheckoutController.cs` (`Pay`, `Receipt`) →
`Services/Implementations/CheckoutService.cs` (`GetCheckoutAsync`,
`PayAsync`, `GetReceiptAsync`) → `Services/Interfaces/IPaymentGateway.cs`,
implemented by `Services/Implementations/SimulatedCardGateway.cs` →
`Repositories/Implementations/PaymentTransactionRepository.cs` →
`PaymentService.ConfirmPaymentAsync`.

**Steps:** see steps 7 to 9 of the end to end trace above.
`SimulatedCardGateway` validates the number (Luhn), expiry and CVC, works
out the brand, and answers the test cards: 4242 4242 4242 4242 and
5555 5555 5555 4444 succeed, 4000 0000 0000 0002 is declined and
4000 0000 0000 9995 has insufficient funds. No bank is contacted and no
money moves.

**Why built this way:** everything that charges a card depends on
`IPaymentGateway`, not on the simulator, so moving to PayFast or Stripe
means registering their implementation in `Program.cs` and nothing else.
Every attempt is kept as a `PaymentTransaction` for an audit trail, but the
card number, expiry and CVC only pass through on their way to the gateway
and are never stored or logged (`PaymentTransactionSecurityTests` checks
the database model for this).

### Refunds

**Files:** `Services/Implementations/RefundService.cs` (`Preview`,
`RefundCancelledBookingAsync`) → `IPaymentGateway.RefundAsync` →
`Controllers/PaymentController.cs` (`MarkRefunded`) →
`PaymentService.MarkRefundedAsync`.

**Steps:** when a paid booking is cancelled, `BookingService.CancelAsync`
calls `RefundCancelledBookingAsync`. A card payment is refunded through the
gateway and marked `Refunded`. An EFT or cash payment is marked
`Refund due`; once the studio has paid it back by hand, the admin presses
Mark refunded on the Payments page. An unpaid booking has nothing to
refund. The cancel page shows the client which of these will happen before
they confirm (`Preview`).

**Why built this way:** the app can only send money back automatically when
it took the money itself (card). For EFT and cash it records what is owed,
so the Payments page's Refund due tab is the studio's to-do list.

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
is looked up by that token (not an id) and passed to the shared
`CancelAsync`, which flips it to `"Cancelled"`, refunds or marks the
payment Refund due (see Refunds), and lets the freed slot trigger
`WaitlistService.PromoteNextInLineAsync`.

**Why built this way:** the link has to work from an email on any device,
without the client signing in first, so there's no cookie to authorize the
action with - the cryptographically random token *is* the credential. See
Design Decisions for the full reasoning.

### Cancelling from the dashboard

**Files:** `BookingController.CancelBooking` (GET, the "Cancel this
booking?" page) and `CancelBookingConfirmed` (POST), both `ClientOnly` →
`BookingService.GetClientCancellationAsync` and `CancelByClientAsync` →
the same `CancelAsync`.

**Steps:** the dashboard's Cancel button opens a confirmation page that
shows the class and what will happen to the payment. Confirming checks the
booking belongs to the signed-in client, is not already cancelled and that
its class hasn't started, then cancels it exactly like the emailed link.
No cancellation email is sent, since the client did it themselves.

**Why built this way:** all three entry points (emailed link, dashboard,
and the admin's `CancelByAdminAsync`) go through one `CancelAsync`, so
refunds and waitlist promotion can't behave differently depending on who
cancelled. Only the admin path emails the client, and it has no button in
the admin pages yet.

### Scheduled reminder and location emails

**Files:** `Services/Implementations/ScheduledNotificationHostedService.cs`
(the timer) → `Services/Implementations/ScheduledNotificationService.cs`
(`SendDueRemindersAsync`, `SendDueLocationEmailsAsync`) →
`NotificationService.SendReminderEmailAsync` / `SendLocationEmailAsync`.

**Steps:** a `BackgroundService` runs once on startup and then every hour;
each run queries for Confirmed bookings meeting the day-before (reminder) or
same-day-and-new-client (location) condition, and skips any booking that
already has a matching `Notification` row logged as Sent. A failed send is
logged as Failed and counted, without stopping the rest of the run, and is
picked up again next hour. The admin dashboard's Send reminders now button
(`AdminController.SendRemindersNow`) runs the same reminder sweep on demand
and reports how many were sent, skipped or failed. "Today" and "tomorrow"
come from the UTC date (see Known limitations).

**Why built this way:** checking `Notifications` for an existing row (rather
than, say, a boolean flag on `Booking`) means the hourly re-run is naturally
idempotent - running it twice in the same hour, or missing a run and
catching up an hour late, can't double-send.

### Admin login

**Files:** `Controllers/AccountController.cs` (`Login`) →
`Services/Implementations/AuthService.cs` (`Login`) → ASP.NET Core cookie
authentication, configured in `Program.cs`.

**Steps:** username/password posted → `AuthService.Login` looks the admin up
by username and checks the password with `BCrypt.Net.BCrypt.Verify`, locking
the account for 15 minutes after 5 failures in a row (unknown usernames are
counted the same way, in memory) → success issues the sign-in cookie with
the username and the Admin role → admin controllers carry
`[Authorize(Policy = AuthPolicies.AdminOnly)]`, which needs that role, so a
signed-in client is sent to the Access denied page.

**Why built this way:** there's exactly one admin account (no sign-up), but
clients now sign in too, so "is someone signed in" is no longer enough. One
cookie scheme with a Role claim, checked by two named policies
(`AdminOnly`, `ClientOnly` in `Authorization/AuthPolicies.cs`), keeps the
rule in one place. `AdminAuthorizationTests` finds every controller action
by reflection and checks it against those policies. Older admin cookies
from before roles existed are given the Admin role on their next request
(`OnValidatePrincipal` in `Program.cs`), so a signed-in admin wasn't logged
out by the change.

### Client dashboard

**Files:** `AccountController.Dashboard` (`ClientOnly`) →
`Services/Implementations/ClientDashboardService.cs` (`GetDashboardAsync`,
static `Build`) and `RewardsService` → `Views/Account/Dashboard.cshtml`.

**Steps:** `RewardsService.EvaluateAsync` runs first, so a class that just
finished counts straight away. `GetDashboardAsync` loads the client's
bookings, payments and waitlist entries, and `Build` sorts them into stat
tiles (upcoming classes, classes attended, total spent, current streak),
upcoming bookings (with Pay now for an unpaid card booking and Cancel),
payments and receipts, past bookings and waitlist entries.

**Why built this way:** `Build` is static and touches no database, so the
rules for what counts as upcoming, attended or spent are unit tested
directly. "Attended" comes from one shared rule, `Services/Attendance.cs`
(not cancelled, class started, paid, not a no-show), which the dashboard,
rewards and the admin Clients page all use, so they can never disagree.

### Rewards: streaks and badges

**Files:** `Services/Implementations/RewardsService.cs` (`EvaluateAsync`,
`GetRewardsAsync`) → `Services/Attendance.cs`, `Services/Streaks.cs`,
`Services/BadgeRules.cs` → the `Badges` and `ClientBadges` tables (badges
seeded by the `AddRewardsAndNoShow` migration).

**Steps:** attended classes give a weekly streak (Monday to Sunday weeks,
the same as the schedule; a week still in progress gets grace, so the
streak only resets after a whole week with no class) and a longest streak.
`BadgeRules` turns those into badges: First Class (1 class), Getting Going
(5), Committed (10), Dedicated (25), Three Week Streak, Eight Week Streak,
Both Studios and Early Bird (a class starting before 09:00). Newly earned
badges are saved to `ClientBadges`. It runs when the dashboard loads, when
an admin confirms a payment, and when an admin changes a no-show.

**Why built this way:** `Streaks` and `BadgeRules` are pure, so every rule
is tested without a database. Badges are only ever added, never removed,
even if a booking is later cancelled or marked a no-show, because taking a
reward back would feel like a punishment.

### Revenue

**Files:** `AdminController.Revenue` and `RevenueCsv` →
`Services/Implementations/RevenueService.cs` (`ResolveRange`,
`GetReportAsync`, static `Build`) → `Services/RevenueChart.cs`,
`Services/ChartScale.cs`, `Services/ClientSpendCsv.cs` →
`Views/Admin/Revenue.cshtml`.

**Steps:** the date range is a preset (all time, this month, last 30 days,
this year) or custom From/To dates. One query loads the payments in range,
and `Build` adds up net revenue, paid count, average per payment and total
refunded, a weekly or monthly series for the line graph, breakdowns by
month, payment method, payment type and venue, and spend per client.
`RevenueCsv` downloads the spend-per-client table as CSV.

**Why built this way:** revenue is money actually kept, so only Confirmed
payments count. Refunded payments are shown on their own and never
counted, and Refund due and Pending aren't revenue at all. Amounts stay
`decimal` throughout. `ClientSpendCsv` quotes every field and defuses any
field starting with `=`, `+`, `-` or `@`, so a client's name can't run as a
spreadsheet formula.

### Other admin pages

**Files:** `AdminController` (`Index`, `Calendar`, `Waitlist`, `Emails`,
`NotifyWaitlistEntry`, `RemoveWaitlistEntry`, `AddToClass`) and
`SessionController` (`Attendees`, `SetNoShow`, `Waitlist`) →
`Services/Implementations/AdminDashboardService.cs`, `WaitlistService` and
`BookingService.SetNoShowAsync`; `ClientsController.Index`.

**Steps:** the Bookings Calendar shows a month of sessions with their
booking counts (each opens the session's edit page). A session's Attendees
page lists who is booked and lets the admin mark a no-show once the class
has started, which stops it counting as attended. The Waitlist page lists
every entry with Notify (a courtesy email), Add to class and Remove. The
Emails page lists the latest 50 entries in `Notifications`, with Sent or
Failed status, filterable by type. The Clients page lists every client with
classes attended, streak and badges, searchable and sortable by attendance.

**Why built this way:** these are read-mostly views, so they live in
`AdminDashboardService` as separate, simply shaped queries, and every
change (a no-show, a waitlist move) goes through the same services the rest
of the app uses.

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
- **PostgreSQL.** Chosen as the relational database, hosted on Neon,
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
- **Docker, with Render, Neon and SendGrid.** The app is containerized
  (see `ThriveWellness/Dockerfile`) and deployed to Render, which builds and
  runs that container and assigns the listening port via the `PORT`
  environment variable at container start (`Program.cs` reads it and only
  overrides the default URL binding when it's set, so local development is
  unaffected). Render's proxy ends HTTPS, so `Program.cs` trusts the
  forwarded headers to keep `Request.Scheme` as `https` (the Google
  redirect URI depends on it). The database is a separate managed
  PostgreSQL instance on Neon, connected to over the network via the
  `DefaultConnection` string. Outbound email goes through SendGrid's API rather than SMTP
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
- **Returning-client identification.** Returning clients are identified by
  email only, so anyone who knows a client's email can see and change their
  name and phone. A verification step, such as an emailed code, is the next
  improvement.
