using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Resources;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class BlockedManager : IBlockedService
    {
        private readonly IBlockedDal _blockedDal;
        private readonly IUserDal _userDal;
        private readonly IImageDal _imageDal;
        private readonly IContentModerationService _contentModeration;

        public BlockedManager(
            IBlockedDal blockedDal,
            IUserDal userDal,
            IImageDal imageDal,
            IContentModerationService contentModeration)
        {
            _blockedDal = blockedDal;
            _userDal = userDal;
            _imageDal = imageDal;
            _contentModeration = contentModeration;
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [ValidationAspect(typeof(CreateBlockedDtoValidator))]
        [TransactionScopeAspect]
        public async Task<IDataResult<BlockedGetDto>> BlockUserAsync(Guid userId, CreateBlockedDto dto)
        {
            // Engellenecek kullanıcı kontrolü
            var targetUser = await _userDal.Get(x => x.Id == dto.BlockedToUserId);
            if (targetUser == null)
                return new ErrorDataResult<BlockedGetDto>(Messages.UserNotFound);

            // Kendini engelleyemez
            if (userId == dto.BlockedToUserId)
                return new ErrorDataResult<BlockedGetDto>("Kendinizi engelleyemezsiniz.");

            // Zaten engellenmiş mi kontrolü
            var existingBlock = await _blockedDal.IsBlockedAsync(userId, dto.BlockedToUserId);
            if (existingBlock)
                return new ErrorDataResult<BlockedGetDto>("Bu kullanıcı zaten engellenmiş.");

            // İçerik moderasyonu kontrolü
            if (!string.IsNullOrWhiteSpace(dto.BlockReason))
            {
                var moderationResult = await _contentModeration.CheckContentAsync(dto.BlockReason);
                if (!moderationResult.Success)
                    return new ErrorDataResult<BlockedGetDto>(moderationResult.Message);
            }

            // Engelleme oluştur
            var blocked = new Blocked
            {
                Id = Guid.NewGuid(),
                BlockedFromUserId = userId,
                BlockedToUserId = dto.BlockedToUserId,
                BlockReason = dto.BlockReason?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _blockedDal.Add(blocked);

            // DTO oluştur
            var result = new BlockedGetDto
            {
                Id = blocked.Id,
                BlockedFromUserId = blocked.BlockedFromUserId,
                BlockedToUserId = blocked.BlockedToUserId,
                BlockReason = blocked.BlockReason,
                CreatedAt = blocked.CreatedAt,
                TargetUserName = $"{targetUser.FirstName} {targetUser.LastName}".Trim(),
                TargetUserType = targetUser.UserType
            };

            // Target user image
            if (targetUser.ImageId.HasValue)
            {
                var image = await _imageDal.Get(x => x.Id == targetUser.ImageId.Value);
                result.TargetUserImage = image?.ImageUrl;
            }

            return new SuccessDataResult<BlockedGetDto>(result, "Kullanıcı başarıyla engellendi.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> UnblockUserAsync(Guid userId, Guid blockedToUserId)
        {
            var success = await _blockedDal.UnblockAsync(userId, blockedToUserId);
            if (!success)
                return new ErrorDataResult<bool>(false, "Engelleme bulunamadı veya kaldırılamadı.");

            return new SuccessDataResult<bool>(true, "Engelleme başarıyla kaldırıldı.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<BlockedGetDto>>> GetMyBlockedUsersAsync(Guid userId)
        {
            var blockedList = await _blockedDal.GetBlockedByUserAsync(userId);
            var result = new List<BlockedGetDto>();

            foreach (var blocked in blockedList)
            {
                var targetUser = await _userDal.Get(x => x.Id == blocked.BlockedToUserId);
                var dto = new BlockedGetDto
                {
                    Id = blocked.Id,
                    BlockedFromUserId = blocked.BlockedFromUserId,
                    BlockedToUserId = blocked.BlockedToUserId,
                    BlockReason = blocked.BlockReason,
                    CreatedAt = blocked.CreatedAt,
                    TargetUserName = targetUser != null ? $"{targetUser.FirstName} {targetUser.LastName}".Trim() : "Bilinmeyen Kullanıcı",
                    TargetUserType = targetUser?.UserType
                };

                if (targetUser?.ImageId != null)
                {
                    var image = await _imageDal.Get(x => x.Id == targetUser.ImageId.Value);
                    dto.TargetUserImage = image?.ImageUrl;
                }

                result.Add(dto);
            }

            return new SuccessDataResult<List<BlockedGetDto>>(result);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<BlockStatusDto>> GetBlockStatusAsync(Guid userId, Guid otherUserId)
        {
            var isBlocked = await _blockedDal.IsBlockedAsync(userId, otherUserId);
            var isBlockedBy = await _blockedDal.IsBlockedAsync(otherUserId, userId);

            return new SuccessDataResult<BlockStatusDto>(new BlockStatusDto
            {
                IsBlocked = isBlocked,
                IsBlockedBy = isBlockedBy
            });
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<HashSet<Guid>>> GetAllBlockedUserIdsAsync(Guid userId)
        {
            var blockedIds = await _blockedDal.GetBlockedUserIdsAsync(userId);
            return new SuccessDataResult<HashSet<Guid>>(blockedIds);
        }
    }
}
