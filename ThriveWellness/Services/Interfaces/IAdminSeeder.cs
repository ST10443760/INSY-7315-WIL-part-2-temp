namespace ThriveWellness.Services.Interfaces
{
    // Ensures exactly one admin account matches configuration (Admin:Username
    // / Admin:Password) exists with the right password hash. Called once at
    // startup - see Program.cs.
    public interface IAdminSeeder
    {
        Task SeedAsync();
    }
}
