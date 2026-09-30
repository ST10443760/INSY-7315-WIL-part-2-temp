using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
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

        public Task CreateAsync(Location location)
        {
            Validate(location);

            return _locationRepository.AddAsync(location);
        }

        public Task UpdateAsync(Location location)
        {
            Validate(location);

            return _locationRepository.UpdateAsync(location);
        }

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
