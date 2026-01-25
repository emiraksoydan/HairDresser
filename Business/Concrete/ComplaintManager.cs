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
using Entities.Concrete.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.Concrete
{
    public class ComplaintManager : IComplaintService
    {
        private readonly IComplaintDal _complaintDal;
        private readonly IUserDal _userDal;
        private readonly IAppointmentDal _appointmentDal;
        private readonly IImageDal _imageDal;
        private readonly IContentModerationService _contentModeration;

        public ComplaintManager(
            IComplaintDal complaintDal,
            IUserDal userDal,
            IAppointmentDal appointmentDal,
            IImageDal imageDal,
            IContentModerationService contentModeration)
        {
            _complaintDal = complaintDal;
            _userDal = userDal;
            _appointmentDal = appointmentDal;
            _imageDal = imageDal;
            _contentModeration = contentModeration;
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [ValidationAspect(typeof(CreateComplaintDtoValidator))]
        [TransactionScopeAspect]
        public async Task<IDataResult<ComplaintGetDto>> CreateComplaintAsync(Guid userId, CreateComplaintDto dto)
        {
            // Şikayet edilecek kullanıcı kontrolü
            var targetUser = await _userDal.Get(x => x.Id == dto.ComplaintToUserId);
            if (targetUser == null)
                return new ErrorDataResult<ComplaintGetDto>(Messages.UserNotFound);

            // Kendine şikayet edemez
            if (userId == dto.ComplaintToUserId)
                return new ErrorDataResult<ComplaintGetDto>("Kendinizi şikayet edemezsiniz.");

            // Eğer appointment varsa, katılımcı kontrolü ve durum kontrolü
            if (dto.AppointmentId.HasValue)
            {
                var appointment = await _appointmentDal.Get(x => x.Id == dto.AppointmentId.Value);
                if (appointment == null)
                    return new ErrorDataResult<ComplaintGetDto>(Messages.AppointmentNotFound);

                // Randevu durumu kontrolü: Completed, Cancelled, Rejected veya Unanswered olmalı
                if (appointment.Status != AppointmentStatus.Completed &&
                    appointment.Status != AppointmentStatus.Cancelled &&
                    appointment.Status != AppointmentStatus.Rejected &&
                    appointment.Status != AppointmentStatus.Unanswered)
                {
                    return new ErrorDataResult<ComplaintGetDto>("Şikayet oluşturmak için randevu tamamlanmış, iptal edilmiş veya cevapsız olmalıdır.");
                }

                // Şikayet eden katılımcı mı kontrolü
                bool isComplainantParticipant = appointment.CustomerUserId == userId ||
                                                appointment.FreeBarberUserId == userId ||
                                                appointment.BarberStoreUserId == userId;
                if (!isComplainantParticipant)
                    return new ErrorDataResult<ComplaintGetDto>("Bu randevunun katılımcısı değilsiniz.");

                // Şikayet edilen katılımcı mı kontrolü
                bool isTargetParticipant = appointment.CustomerUserId == dto.ComplaintToUserId ||
                                           appointment.FreeBarberUserId == dto.ComplaintToUserId ||
                                           appointment.BarberStoreUserId == dto.ComplaintToUserId;
                if (!isTargetParticipant)
                    return new ErrorDataResult<ComplaintGetDto>("Şikayet edilen kişi bu randevunun katılımcısı değil.");
            }

            // Aynı kullanıcıya aynı randevudan daha önce şikayet yapılmış mı
            var existingComplaint = await _complaintDal.ExistsAsync(userId, dto.ComplaintToUserId, dto.AppointmentId);
            if (existingComplaint)
                return new ErrorDataResult<ComplaintGetDto>("Bu kullanıcıyı zaten şikayet ettiniz.");

            // İçerik moderasyonu kontrolü
            if (!string.IsNullOrWhiteSpace(dto.ComplaintReason))
            {
                var moderationResult = await _contentModeration.CheckContentAsync(dto.ComplaintReason);
                if (!moderationResult.Success)
                    return new ErrorDataResult<ComplaintGetDto>(moderationResult.Message);
            }

            // Şikayet oluştur
            var complaint = new Complaint
            {
                Id = Guid.NewGuid(),
                ComplaintFromUserId = userId,
                ComplaintToUserId = dto.ComplaintToUserId,
                AppointmentId = dto.AppointmentId,
                ComplaintReason = dto.ComplaintReason?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _complaintDal.Add(complaint);

            // DTO oluştur
            var result = new ComplaintGetDto
            {
                Id = complaint.Id,
                ComplaintFromUserId = complaint.ComplaintFromUserId,
                ComplaintToUserId = complaint.ComplaintToUserId,
                AppointmentId = complaint.AppointmentId,
                ComplaintReason = complaint.ComplaintReason,
                CreatedAt = complaint.CreatedAt,
                TargetUserName = $"{targetUser.FirstName} {targetUser.LastName}".Trim(),
                TargetUserType = targetUser.UserType
            };

            // Target user image
            if (targetUser.ImageId.HasValue)
            {
                var image = await _imageDal.Get(x => x.Id == targetUser.ImageId.Value);
                result.TargetUserImage = image?.ImageUrl;
            }

            return new SuccessDataResult<ComplaintGetDto>(result, "Şikayet başarıyla oluşturuldu.");
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<ComplaintGetDto>>> GetMyComplaintsAsync(Guid userId)
        {
            var complaints = await _complaintDal.GetByUserAsync(userId);
            var result = new List<ComplaintGetDto>();

            foreach (var complaint in complaints)
            {
                var targetUser = await _userDal.Get(x => x.Id == complaint.ComplaintToUserId);
                var dto = new ComplaintGetDto
                {
                    Id = complaint.Id,
                    ComplaintFromUserId = complaint.ComplaintFromUserId,
                    ComplaintToUserId = complaint.ComplaintToUserId,
                    AppointmentId = complaint.AppointmentId,
                    ComplaintReason = complaint.ComplaintReason,
                    CreatedAt = complaint.CreatedAt,
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

            return new SuccessDataResult<List<ComplaintGetDto>>(result);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect]
        public async Task<IDataResult<bool>> DeleteComplaintAsync(Guid userId, Guid complaintId)
        {
            var complaint = await _complaintDal.Get(x => x.Id == complaintId && !x.IsDeleted);
            if (complaint == null)
                return new ErrorDataResult<bool>(false, "Şikayet bulunamadı.");

            if (complaint.ComplaintFromUserId != userId)
                return new ErrorDataResult<bool>(false, "Bu şikayeti silme yetkiniz yok.");

            // Soft delete
            complaint.IsDeleted = true;
            complaint.DeletedAt = DateTime.UtcNow;
            await _complaintDal.Update(complaint);
            return new SuccessDataResult<bool>(true, "Şikayet başarıyla silindi.");
        }
    }
}
