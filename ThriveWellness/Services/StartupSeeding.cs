using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace ThriveWellness.Services
{
    // Runs a startup seeding step (Program.cs uses it for AdminSeeder) so
    // that an unreachable database can't take the whole process down. An
    // uncaught exception here used to exit the app, which on Render meant a
    // restart loop for the entire site rather than just a broken admin
    // login. Each attempt that fails with a database error is logged as a
    // warning and retried after a short pause; once the attempts run out the
    // app carries on starting anyway, and the seed simply runs again on the
    // next deploy or restart.
    public static class StartupSeeding
    {
        public const int MaxAttempts = 5;
        public static readonly TimeSpan DelayBetweenAttempts = TimeSpan.FromSeconds(2);

        // Returns true if the seed step succeeded, false if every attempt
        // failed with a database error. Anything that isn't a database error
        // (a genuine bug, for example) still propagates as before. The delay
        // is a parameter only so the tests don't have to wait for real.
        public static async Task<bool> RunWithRetryAsync(
            Func<Task> seed,
            ILogger logger,
            int maxAttempts = MaxAttempts,
            TimeSpan? delayBetweenAttempts = null)
        {
            var delay = delayBetweenAttempts ?? DelayBetweenAttempts;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await seed();
                    return true;
                }
                catch (Exception ex) when (FindDatabaseError(ex) is { } dbError)
                {
                    // Type and message only - never the exception object
                    // itself, its stack trace or anything from
                    // configuration, so a connection string can't end up
                    // in the logs.
                    logger.LogWarning(
                        "Startup seeding failed (attempt {Attempt} of {MaxAttempts}): {ExceptionType}: {ExceptionMessage}",
                        attempt, maxAttempts, dbError.GetType().Name, dbError.Message);

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(delay);
                    }
                }
            }

            logger.LogWarning(
                "Startup seeding gave up after {MaxAttempts} attempts - starting the app without it.",
                maxAttempts);
            return false;
        }

        // NpgsqlException (connection refused, DNS failure, SSL, server
        // errors) derives from DbException; DbUpdateException covers a
        // failed SaveChanges; TimeoutException covers a connect timeout.
        // The inner exceptions are searched too, because Npgsql's default
        // execution strategy wraps a transient connection failure in an
        // InvalidOperationException ("likely due to a transient failure")
        // even with retry-on-failure off. Returns the database exception
        // found, or null if this isn't a database error at all.
        private static Exception? FindDatabaseError(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is DbException or DbUpdateException or TimeoutException)
                {
                    return current;
                }
            }
            return null;
        }
    }
}
