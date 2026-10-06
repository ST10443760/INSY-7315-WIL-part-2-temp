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
