using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Enums
{
    public enum NotificationType
    {
        AppointmentCreated,       
        AppointmentApproved,
        AppointmentRejected,
        AppointmentCancelled,
        AppointmentCompleted,
        AppointmentUnanswered,
        AppointmentDecisionUpdated,
        
        // 3'lü sistem için yeni bildirim tipleri
        FreeBarberRejectedInitial,      // FreeBarber ilk isteği reddetti (Müşteri'ye)
        StoreRejectedSelection,          // Store seçimi reddetti (FreeBarber+Müşteri'ye)
        StoreApprovedSelection,          // Store onayladı (FreeBarber+Müşteri'ye)
        StoreSelectionTimeout,           // Store 5dk cevap vermedi (FreeBarber+Müşteri'ye)
        CustomerRejectedFinal,           // Müşteri final red verdi (FreeBarber+Store'a)
        CustomerApprovedFinal,           // Müşteri final onay verdi (FreeBarber+Store'a)
        CustomerFinalTimeout,            // Müşteri 30dk içinde cevap vermedi (Herkes'e)
    }
}
