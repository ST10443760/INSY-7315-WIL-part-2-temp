using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: CRUD for studio locations, with one rule layered on top
    // of the repository - a location can't be deleted while any session
    // (past or future) still references it.
    public class LocationService : ILocationService
    {
        private readonly ILocationRepository _locationRepository;
        private readonly ISessionRepository _sessionRepository;

        public LocationService(ILocationRepository locationRepository, ISessionRepository sessionRepository)
        {
            _locationRepository = locationRepository;
            _sessionRepository = sessionRepository;
        }

        public Task<IEnumerable<Location>> GetAllAsync()
        {
            return _locationRepository.GetAllAsync();
        }

        public Task<Location?> GetByIdAsync(int id)
        {
            return _locationRepository.GetByIdAsync(id);
        }

        // Creates a new location after checking it has a name and address.
        public Task CreateAsync(Location location)
        {
            Validate(location);

            return _locationRepository.AddAsync(location);
        }

        // Updates an existing location after the same name/address
        // validation as create.
        public Task UpdateAsync(Location location)
        {
            Validate(location);

            return _locationRepository.UpdateAsync(location);
        }

        // Deletes a location - blocked (see the check below) if any session
        // still points at it, past or future.
        public async Task DeleteAsync(int id)
        {
            // A session (past or future) keeps this location referenced for
            // its own record, so removing the location out from under it
            // would either fail as a DB constraint violation or, worse,
            // silently orphan the reference - block it here instead with a
            // message an admin can actually act on.
            if (await _sessionRepository.AnyForLocationAsync(id))
            {
                throw new ArgumentException("This location has sessions (past or future) tied to it and can't be deleted.");
            }

            await _locationRepository.DeleteAsync(id);
        }

        // Shared validation for create and update: name and address are the
        // two fields every other screen displays, so neither can be blank.
        private static void Validate(Location location)
        {
            if (string.IsNullOrWhiteSpace(location.Name))
            {
                throw new ArgumentException("Name is required.");
            }

            if (string.IsNullOrWhiteSpace(location.Address))
            {
                throw new ArgumentException("Address is required.");
            }
        }
    }
}
