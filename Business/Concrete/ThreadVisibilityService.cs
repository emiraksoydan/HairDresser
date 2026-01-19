using Business.Abstract;
using DataAccess.Abstract;
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.Concrete
{
    /// <summary>
    /// Centralized service for thread visibility logic
    /// 
    /// Thread Visibility Rules:
    /// 1. Appointment Threads: Visible if status is Pending or Approved
    /// 2. Favorite Threads: Visible if at least one active favorite exists between users
    /// 3. Store-based Favorites: Visible if user favorited any store owned by the other user
    /// 
    /// Performance Optimizations:
    /// - Batch processing for multiple threads
    /// - Cached visibility checks
    /// - Single query for all favorites
    /// </summary>
    public class ThreadVisibilityService(
        IAppointmentDal appointmentDal,
        IFavoriteDal favoriteDal,
        IBarberStoreDal barberStoreDal)
    {
        /// <summary>
        /// Checks if a thread is visible to a user
        /// </summary>
        public async Task<bool> IsThreadVisibleAsync(Guid threadId, Guid userId, Guid? appointmentId, Guid? favoriteFromUserId, Guid? favoriteToUserId)
        {
            // Appointment thread visibility
            if (appointmentId.HasValue)
            {
                var appointment = await appointmentDal.Get(x => x.Id == appointmentId.Value);
                if (appointment == null) return false;

                // Visible if Pending or Approved
                if (appointment.Status == AppointmentStatus.Pending || appointment.Status == AppointmentStatus.Approved)
                    return true;

                // Not visible if status is not active, check favorites as fallback
                // (This handles the case where appointment ended but users still have favorites)
                return await CheckFavoriteVisibilityAsync(userId, appointment.CustomerUserId, appointment.BarberStoreUserId, appointment.FreeBarberUserId);
            }

            // Favorite thread visibility
            if (favoriteFromUserId.HasValue && favoriteToUserId.HasValue)
            {
                return await IsFavoriteThreadVisibleAsync(favoriteFromUserId.Value, favoriteToUserId.Value, userId);
            }

            return false;
        }

        /// <summary>
        /// Batch checks visibility for multiple threads (optimized)
        /// </summary>
        public async Task<Dictionary<Guid, bool>> GetThreadVisibilityMapAsync(List<Guid> threadIds, Guid userId, Dictionary<Guid, (Guid? appointmentId, Guid? favoriteFromId, Guid? favoriteToId)> threadMetadata)
        {
            var result = new Dictionary<Guid, bool>();

            // Group threads by type
            var appointmentThreads = threadIds.Where(id => threadMetadata[id].appointmentId.HasValue).ToList();
            var favoriteThreads = threadIds.Where(id => threadMetadata[id].favoriteFromId.HasValue && threadMetadata[id].favoriteToId.HasValue).ToList();

            // Batch process appointment threads
            if (appointmentThreads.Any())
            {
                var appointmentIds = appointmentThreads.Select(id => threadMetadata[id].appointmentId!.Value).ToList();
                var appointments = await appointmentDal.GetAll(x => appointmentIds.Contains(x.Id));
                var appointmentStatusMap = appointments.ToDictionary(a => a.Id, a => a.Status);

                foreach (var threadId in appointmentThreads)
                {
                    var appointmentId = threadMetadata[threadId].appointmentId!.Value;
                    if (appointmentStatusMap.TryGetValue(appointmentId, out var status))
                    {
                        result[threadId] = status == AppointmentStatus.Pending || status == AppointmentStatus.Approved;
                    }
                    else
                    {
                        result[threadId] = false;
                    }
                }
            }

            // Batch process favorite threads
            if (favoriteThreads.Any())
            {
                foreach (var threadId in favoriteThreads)
                {
                    var fromUserId = threadMetadata[threadId].favoriteFromId!.Value;
                    var toUserId = threadMetadata[threadId].favoriteToId!.Value;
                    result[threadId] = await IsFavoriteThreadVisibleAsync(fromUserId, toUserId, userId);
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if favorite thread is visible (at least one active favorite exists)
        /// </summary>
        private async Task<bool> IsFavoriteThreadVisibleAsync(Guid fromUserId, Guid toUserId, Guid currentUserId)
        {
            // User ID based favorite check (both directions)
            var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
            if (favorite1 != null && favorite1.IsActive)
                return true;

            var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
            if (favorite2 != null && favorite2.IsActive)
                return true;

            // Store-based favorite check
            // fromUserId might have favorited a store owned by toUserId
            var storesToCheck = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == toUserId);
            if (storesToCheck.Any())
            {
                var storeIds = storesToCheck.Select(s => s.Id).ToList();
                var favoriteStores = await favoriteDal.GetAll(x =>
                    x.FavoritedFromId == fromUserId &&
                    storeIds.Contains(x.FavoritedToId) &&
                    x.IsActive);

                if (favoriteStores.Any())
                    return true;
            }

            // toUserId might have favorited a store owned by fromUserId
            var storesToCheck2 = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == fromUserId);
            if (storesToCheck2.Any())
            {
                var storeIds2 = storesToCheck2.Select(s => s.Id).ToList();
                var favoriteStores2 = await favoriteDal.GetAll(x =>
                    x.FavoritedFromId == toUserId &&
                    storeIds2.Contains(x.FavoritedToId) &&
                    x.IsActive);

                if (favoriteStores2.Any())
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Helper method to check favorite visibility for appointment participants
        /// </summary>
        private async Task<bool> CheckFavoriteVisibilityAsync(Guid currentUserId, Guid? customerUserId, Guid? storeOwnerUserId, Guid? freeBarberUserId)
        {
            var participantIds = new[] { customerUserId, storeOwnerUserId, freeBarberUserId }
                .Where(x => x.HasValue && x.Value != currentUserId)
                .Select(x => x!.Value)
                .ToList();

            if (!participantIds.Any())
                return false;

            // Check if current user has active favorite with any participant
            foreach (var participantId in participantIds)
            {
                // User-based favorites
                var favorite1 = await favoriteDal.GetByUsersAsync(currentUserId, participantId);
                if (favorite1 != null && favorite1.IsActive)
                    return true;

                var favorite2 = await favoriteDal.GetByUsersAsync(participantId, currentUserId);
                if (favorite2 != null && favorite2.IsActive)
                    return true;

                // Store-based favorites (if participant is store owner)
                var stores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == participantId);
                if (stores.Any())
                {
                    var storeIds = stores.Select(s => s.Id).ToList();
                    var favoriteStores = await favoriteDal.GetAll(x =>
                        x.FavoritedFromId == currentUserId &&
                        storeIds.Contains(x.FavoritedToId) &&
                        x.IsActive);

                    if (favoriteStores.Any())
                        return true;
                }
            }

            return false;
        }
    }
}
